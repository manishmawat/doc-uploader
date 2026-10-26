// Directional input for Components/Games/Game2048View.razor: arrow keys / WASD while the
// board has focus, and swipes (pointer events, so mouse drags work too).
// Only direction keys are prevented, so Tab and other keys keep working.

const KEYS = {
    ArrowUp: 'Up', ArrowDown: 'Down', ArrowLeft: 'Left', ArrowRight: 'Right',
    w: 'Up', s: 'Down', a: 'Left', d: 'Right',
    W: 'Up', S: 'Down', A: 'Left', D: 'Right',
};
const MIN_SWIPE_PX = 30;
const detachers = new WeakMap();

export function attachDirectionalInput(element, dotNetRef) {
    detachDirectionalInput(element);

    const send = (direction) => dotNetRef.invokeMethodAsync('OnDirection', direction);

    const onKeyDown = (event) => {
        const direction = KEYS[event.key];
        if (!direction || event.altKey || event.ctrlKey || event.metaKey) {
            return;
        }
        event.preventDefault();
        send(direction);
    };

    let start = null;
    const onPointerDown = (event) => {
        start = { x: event.clientX, y: event.clientY };
        element.focus();
    };
    const onPointerUp = (event) => {
        if (!start) {
            return;
        }
        const dx = event.clientX - start.x;
        const dy = event.clientY - start.y;
        start = null;
        if (Math.max(Math.abs(dx), Math.abs(dy)) < MIN_SWIPE_PX) {
            return;
        }
        send(Math.abs(dx) > Math.abs(dy) ? (dx > 0 ? 'Right' : 'Left') : (dy > 0 ? 'Down' : 'Up'));
    };
    const onPointerCancel = () => { start = null; };

    element.addEventListener('keydown', onKeyDown);
    element.addEventListener('pointerdown', onPointerDown);
    element.addEventListener('pointerup', onPointerUp);
    element.addEventListener('pointercancel', onPointerCancel);

    detachers.set(element, () => {
        element.removeEventListener('keydown', onKeyDown);
        element.removeEventListener('pointerdown', onPointerDown);
        element.removeEventListener('pointerup', onPointerUp);
        element.removeEventListener('pointercancel', onPointerCancel);
    });
}

export function detachDirectionalInput(element) {
    detachers.get(element)?.();
    detachers.delete(element);
}

export function focus(element) {
    element?.focus({ preventScroll: true });
}
