import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-saas-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './saas-shell.html',
  styleUrl: './saas-shell.scss',
})
export class SaasShell {
  private readonly _auth = inject(AuthService);
  private readonly _router = inject(Router);

  logout(): void {
    this._auth.logout();
    void this._router.navigate(['/auth/login']);
  }
}
