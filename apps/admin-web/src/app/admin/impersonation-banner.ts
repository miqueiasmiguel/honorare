import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-impersonation-banner',
  templateUrl: './impersonation-banner.html',
  styleUrl: './impersonation-banner.scss',
})
export class ImpersonationBanner {
  protected readonly auth = inject(AuthService);
  private readonly _router = inject(Router);

  exitImpersonation(): void {
    this.auth.exitImpersonation().subscribe({
      next: () => {
        void this._router.navigate(['/saas']);
      },
      error: () => undefined,
    });
  }
}
