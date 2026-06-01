import { Component, computed, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { BottomNavComponent } from './bottom-nav/bottom-nav';

@Component({
  selector: 'app-painel',
  imports: [RouterOutlet, BottomNavComponent],
  template: `
    <div class="painel-layout">
      <header class="painel-topbar">
        <span class="painel-topbar__wordmark">honorare</span>
        @if (userEmail()) {
          <div class="painel-topbar__user">
            <span class="painel-topbar__email">{{ userEmail() }}</span>
            <span class="painel-topbar__avatar" aria-hidden="true">{{ userInitial() }}</span>
          </div>
        }
      </header>
      <main class="painel-content">
        <router-outlet />
      </main>
      <app-bottom-nav />
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .painel-layout {
        display: flex;
        flex-direction: column;
        min-height: 100svh;
      }

      .painel-topbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: calc(env(safe-area-inset-top) + 12px) 16px 12px;
        background-color: var(--color-tinta);
        min-height: calc(env(safe-area-inset-top) + 52px);
        flex-shrink: 0;
      }

      .painel-topbar__wordmark {
        font-family: var(--font-mono);
        font-size: 18px;
        font-weight: 500;
        color: var(--color-terracota);
        letter-spacing: -0.01em;
      }

      .painel-topbar__user {
        display: flex;
        align-items: center;
        gap: 10px;
        min-width: 0;
      }

      .painel-topbar__email {
        font-family: var(--font-sans);
        font-size: 13px;
        color: var(--color-tinta-terciaria);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        max-width: 160px;
      }

      .painel-topbar__avatar {
        flex-shrink: 0;
        width: 30px;
        height: 30px;
        border-radius: 50%;
        background-color: var(--color-terracota);
        color: var(--color-pergaminho-claro);
        font-family: var(--font-sans);
        font-size: 13px;
        font-weight: 600;
        display: flex;
        align-items: center;
        justify-content: center;
        text-transform: uppercase;
      }

      .painel-content {
        flex: 1;
        padding-bottom: calc(56px + env(safe-area-inset-bottom));
        min-height: 0;
      }
    `,
  ],
})
export class Painel {
  readonly userEmail = inject(AuthService).userEmail;

  readonly userInitial = computed(() => {
    const email = this.userEmail();
    if (!email) return '';
    return email[0].toUpperCase();
  });
}
