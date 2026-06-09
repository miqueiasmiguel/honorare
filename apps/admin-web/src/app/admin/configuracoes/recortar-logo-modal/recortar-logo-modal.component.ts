import { Component, input, output, signal } from '@angular/core';
import { ImageCropperComponent, type ImageCroppedEvent } from 'ngx-image-cropper';

// Proporção fixa do recorte (3:2). Acomoda emblemas/brasões e logos quadrados
// com pouco corte. O PDF de recurso preserva a proporção via FitHeight, então
// este valor controla apenas o enquadramento — ajuste aqui se o layout mudar.
const ASPECT_RATIO = 3 / 2;
// Limita o lado maior do PNG resultante para mantê-lo bem abaixo do teto de 2 MB.
const RESIZE_TO_WIDTH = 600;
const MAX_BYTES = 2 * 1024 * 1024;

@Component({
  selector: 'app-recortar-logo-modal',
  imports: [ImageCropperComponent],
  templateUrl: './recortar-logo-modal.component.html',
  styleUrl: './recortar-logo-modal.component.scss',
})
export class RecortarLogoModalComponent {
  readonly arquivo = input<File | null>(null);

  readonly recortado = output<File>();
  readonly cancelado = output();

  readonly aspectRatio = ASPECT_RATIO;
  readonly resizeToWidth = RESIZE_TO_WIDTH;
  readonly erro = signal('');

  private _blob: Blob | null = null;

  aoRecortar(event: ImageCroppedEvent): void {
    this._blob = event.blob ?? null;
  }

  confirmar(): void {
    const blob = this._blob;
    if (blob === null) {
      this.erro.set('Aguarde o processamento da imagem.');
      return;
    }
    if (blob.size > MAX_BYTES) {
      this.erro.set('Imagem recortada muito grande. Máximo 2 MB.');
      return;
    }
    this.erro.set('');
    this._blob = null;
    this.recortado.emit(new File([blob], 'logo.png', { type: 'image/png' }));
  }

  cancelar(): void {
    this._blob = null;
    this.erro.set('');
    this.cancelado.emit();
  }
}
