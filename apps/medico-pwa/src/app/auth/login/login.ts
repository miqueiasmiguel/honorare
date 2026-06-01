import { Component } from '@angular/core';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  readonly _callbackUrl = environment.googleAuthCallbackUrl;

  navigateToGoogle(): void {
    const returnUrl = encodeURIComponent(this._callbackUrl);
    window.location.href = `/api/v1/auth/google?returnUrl=${returnUrl}`;
  }
}
