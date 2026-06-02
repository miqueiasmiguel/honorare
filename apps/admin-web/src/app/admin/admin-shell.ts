import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { ImpersonationBanner } from './impersonation-banner';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ImpersonationBanner],
  templateUrl: './admin-shell.html',
  styleUrl: './admin-shell.scss',
})
export class AdminShell {
  private readonly _auth = inject(AuthService);
  private readonly _router = inject(Router);

  logout(): void {
    this._auth.logout();
    void this._router.navigate(['/auth/login']);
  }
}
