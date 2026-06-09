import { TestBed } from '@angular/core/testing';
import type { ImageCroppedEvent } from 'ngx-image-cropper';
import { RecortarLogoModalComponent } from './recortar-logo-modal.component';

function setup() {
  TestBed.configureTestingModule({ imports: [RecortarLogoModalComponent] });
  const fixture = TestBed.createComponent(RecortarLogoModalComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance };
}

function cropEvent(blob: Blob | null): ImageCroppedEvent {
  return { blob, width: 200, height: 64 } as unknown as ImageCroppedEvent;
}

describe('RecortarLogoModalComponent', () => {
  it('não renderiza o backdrop quando arquivo é null', () => {
    const { fixture } = setup();
    const backdrop = (fixture.nativeElement as HTMLElement).querySelector(
      '.recortar-logo-modal__backdrop',
    );
    expect(backdrop).toBeNull();
  });

  it('confirmar emite File PNG a partir do blob recortado', () => {
    const { component } = setup();
    let emitido: File | null = null;
    component.recortado.subscribe((f) => {
      emitido = f;
    });

    component.aoRecortar(cropEvent(new Blob(['img'], { type: 'image/png' })));
    component.confirmar();

    expect(emitido).not.toBeNull();
    const file = emitido as unknown as File;
    expect(file.type).toBe('image/png');
    expect(file.name).toBe('logo.png');
  });

  it('confirmar sem blob processado seta erro e não emite', () => {
    const { component } = setup();
    const spy = vi.fn();
    component.recortado.subscribe(spy);

    component.confirmar();

    expect(spy).not.toHaveBeenCalled();
    expect(component.erro()).not.toBe('');
  });

  it('confirmar com imagem acima de 2 MB seta erro e não emite', () => {
    const { component } = setup();
    const spy = vi.fn();
    component.recortado.subscribe(spy);

    const big = { size: 3 * 1024 * 1024, type: 'image/png' } as Blob;
    component.aoRecortar(cropEvent(big));
    component.confirmar();

    expect(spy).not.toHaveBeenCalled();
    expect(component.erro()).toContain('2 MB');
  });

  it('cancelar emite o evento cancelado', () => {
    const { component } = setup();
    const spy = vi.fn();
    component.cancelado.subscribe(spy);

    component.cancelar();

    expect(spy).toHaveBeenCalled();
  });
});
