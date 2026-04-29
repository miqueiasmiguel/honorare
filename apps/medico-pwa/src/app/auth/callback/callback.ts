import { Component, inject, OnInit } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-callback',
  template: '<p>A autenticar...</p>',
})
export class Callback implements OnInit {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _location = inject(Location);
  private readonly _auth = inject(AuthService);

  ngOnInit(): void {
    const params: Record<string, string | undefined> = this._route.snapshot.queryParams;
    const accessToken = params['accessToken'];
    const refreshToken = params['refreshToken'];
    const expiresInRaw = params['expiresIn'];

    if (!accessToken || !refreshToken || !expiresInRaw) {
      void this._router.navigate(['/auth/login']);
      return;
    }

    // Strip tokens from browser history BEFORE storing them — prevents leakage
    // via the Back button, browser history sync, or referer headers.
    const cleanUrl = this._router.url.split('?')[0];
    this._location.replaceState(cleanUrl);

    this._auth.storeTokens({
      accessToken,
      refreshToken,
      expiresIn: +expiresInRaw,
    });

    void this._router.navigate(['/']);
  }
}
