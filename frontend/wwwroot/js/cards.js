// Helpers for Pages/Cards.razor: read a barcode photo, take a card photo, keep card photos on the
// device, keep the screen on while a card is shown, and remove the on-device copies on sign-out.

// The browser's own barcode detector (Chrome on Android, some desktops) -> ZXing format names
const NATIVE_FORMATS = {
    code_128: 'CODE_128', code_39: 'CODE_39', code_93: 'CODE_93', codabar: 'CODABAR',
    ean_13: 'EAN_13', ean_8: 'EAN_8', upc_a: 'UPC_A', upc_e: 'UPC_E', itf: 'ITF',
    qr_code: 'QR_CODE', pdf417: 'PDF_417', data_matrix: 'DATA_MATRIX', aztec: 'AZTEC',
};
const MAX_SIDE = 1600;       // larger photos are scaled down before ZXing reads them
const PHOTO_MAX_SIDE = 1280; // card photos are stored at most this size...
const PHOTO_QUALITY = 0.82;  // ...as JPEG at this quality (about 100-200 KB)
const PHOTO_CACHE = 'uploaderdoc-card-photos';

/**
 * Reads the photo chosen in a file input. Returns { photo, format, value } when the browser's own
 * detector found a barcode, otherwise { photo, width, height, luminance } (greyscale pixels) for
 * ZXing to read, or null when no file was chosen. photo is the picture as a resized JPEG, so it can
 * also be kept as the card's photo.
 */
export async function readBarcodeImage(input) {
    const file = takeFile(input);
    if (!file) {
        return null;
    }

    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    try {
        const photo = await toJpeg(bitmap);
        const native = await detectNatively(bitmap);
        if (native) {
            return { photo, ...native };
        }

        const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
        const width = Math.max(1, Math.round(bitmap.width * scale));
        const height = Math.max(1, Math.round(bitmap.height * scale));
        const context = drawScaled(bitmap, width, height);

        const rgba = context.getImageData(0, 0, width, height).data;
        const luminance = new Uint8Array(width * height);
        for (let i = 0, p = 0; p < luminance.length; i += 4, p++) {
            luminance[p] = (rgba[i] * 299 + rgba[i + 1] * 587 + rgba[i + 2] * 114) / 1000;
        }
        return { photo, width, height, luminance };
    } finally {
        bitmap.close();
    }
}

/** Reads the photo chosen in a file input as a resized JPEG (Uint8Array), or null when none was chosen. */
export async function readCardPhoto(input) {
    const file = takeFile(input);
    if (!file) {
        return null;
    }
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
    try {
        return await toJpeg(bitmap);
    } finally {
        bitmap.close();
    }
}

function takeFile(input) {
    const file = input.files?.[0] ?? null;
    input.value = ''; // lets the same photo be chosen again
    return file;
}

function drawScaled(bitmap, width, height) {
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d', { willReadFrequently: true });
    context.drawImage(bitmap, 0, 0, width, height);
    return context;
}

async function toJpeg(bitmap) {
    const scale = Math.min(1, PHOTO_MAX_SIDE / Math.max(bitmap.width, bitmap.height));
    const width = Math.max(1, Math.round(bitmap.width * scale));
    const height = Math.max(1, Math.round(bitmap.height * scale));
    const canvas = drawScaled(bitmap, width, height).canvas;
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', PHOTO_QUALITY));
    return new Uint8Array(await blob.arrayBuffer());
}

async function detectNatively(bitmap) {
    if (!('BarcodeDetector' in window)) {
        return null;
    }
    try {
        const supported = (await BarcodeDetector.getSupportedFormats()).filter(f => NATIVE_FORMATS[f]);
        if (supported.length === 0) {
            return null;
        }
        const codes = await new BarcodeDetector({ formats: supported }).detect(bitmap);
        const code = codes.find(c => NATIVE_FORMATS[c.format] && c.rawValue);
        return code ? { format: NATIVE_FORMATS[code.format], value: code.rawValue } : null;
    } catch {
        return null; // fall back to ZXing
    }
}

// --- Card photos kept on the device (Cache Storage: far more room than localStorage) ---
// Keys look like /card-photos/<user>/<card id>/<version>; storing a new version removes older ones.

/** Returns an object URL for a JPEG held in memory (e.g. a photo not saved yet). */
export function photoUrl(bytes) {
    return URL.createObjectURL(new Blob([bytes], { type: 'image/jpeg' }));
}

/** Returns an object URL for the cached photo at key, or null when it isn't on this device. */
export async function cachedPhotoUrl(key) {
    try {
        const response = await (await caches.open(PHOTO_CACHE)).match(key);
        return response ? URL.createObjectURL(await response.blob()) : null;
    } catch {
        return null;
    }
}

/** Keeps the photo on the device under key (replacing other versions of the same card) and returns an object URL. */
export async function storePhoto(key, bytes) {
    const blob = new Blob([bytes], { type: 'image/jpeg' });
    try {
        const cache = await caches.open(PHOTO_CACHE);
        await removeCachedPhotos(cache, cardPrefix(key));
        await cache.put(key, new Response(blob, { headers: { 'Content-Type': 'image/jpeg' } }));
    } catch {
        // storage blocked or full: the photo still shows now, just not offline
    }
    return URL.createObjectURL(blob);
}

/** Removes every cached version of one card's photo (key prefix /card-photos/<user>/<card id>/). */
export async function forgetPhoto(prefix) {
    try {
        await removeCachedPhotos(await caches.open(PHOTO_CACHE), prefix);
    } catch {
        // nothing cached
    }
}

export function revokeUrl(url) {
    if (url) {
        URL.revokeObjectURL(url);
    }
}

function cardPrefix(key) {
    return key.substring(0, key.lastIndexOf('/') + 1);
}

async function removeCachedPhotos(cache, prefix) {
    const absolute = new URL(prefix, location.origin).href;
    for (const request of await cache.keys()) {
        if (request.url.startsWith(absolute)) {
            await cache.delete(request);
        }
    }
}

export function isOnline() {
    return navigator.onLine;
}

let wakeLock = null;

/** Asks the device not to dim or lock the screen while a card is shown (where supported). */
export async function keepScreenOn() {
    try {
        wakeLock = await navigator.wakeLock?.request('screen') ?? null;
    } catch {
        wakeLock = null;
    }
}

export async function allowScreenOff() {
    try {
        await wakeLock?.release();
    } catch {
        // already released
    }
    wakeLock = null;
}

/** Removes every user's on-device copy of their cards and card photos (called on sign-out). */
export async function clearCardCaches(prefix) {
    try {
        Object.keys(localStorage).filter(k => k.startsWith(prefix)).forEach(k => localStorage.removeItem(k));
    } catch {
        // storage blocked: nothing was saved
    }
    try {
        await caches.delete(PHOTO_CACHE);
    } catch {
        // nothing cached
    }
}
