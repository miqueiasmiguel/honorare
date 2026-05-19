import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-painel',
  imports: [RouterOutlet],
  template: `
    <div class="painel-layout">
      <header class="painel-topbar">
        <span class="painel-topbar__wordmark">honorare</span>
        @if (userEmail()) {
          <span class="painel-topbar__user">{{ userEmail() }}</span>
        }
      </header>
      <main class="painel-content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      .painel-layout {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }

      .painel-topbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 12px 16px;
        background-color: var(--color-tinta);
      }

      .painel-topbar__wordmark {
        font-family: var(--font-mono);
        font-size: 18px;
        font-weight: 500;
        color: var(--color-terracota);
        letter-spacing: -0.01em;
      }

      .painel-topbar__user {
        font-family: var(--font-sans);
        font-size: 13px;
        color: var(--color-borda-discreta);
      }

      .painel-content {
        flex: 1;
      }
    `,
  ],
})
export class Painel {
  readonly userEmail = inject(AuthService).userEmail;
}
