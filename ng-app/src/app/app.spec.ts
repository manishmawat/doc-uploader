import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { MyDocuments } from './documents/my-documents';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows Files and Recycle bin tabs', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const tabs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.tabs a'));
    expect(tabs.map((a) => a.textContent?.trim())).toEqual(['Files', 'Recycle bin']);
  });

  it('asks to be opened from the host app when not framed', async () => {
    const fixture = TestBed.createComponent(MyDocuments);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.notice')?.textContent).toContain('Document Uploader app');
  });
});
