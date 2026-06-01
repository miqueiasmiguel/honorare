import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="bottom-nav" aria-label="Navegação principal">
      <a
        class="bottom-nav__item"
        routerLink="/guias"
        routerLinkActive="bottom-nav__item--active"
        aria-label="Guias"
      >
        <svg
          class="bottom-nav__icon"
          width="24"
          height="24"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.75"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <line x1="16" y1="13" x2="8" y2="13" />
          <line x1="16" y1="17" x2="8" y2="17" />
        </svg>
        <span class="bottom-nav__label">Guias</span>
      </a>
      <a
        class="bottom-nav__item"
        routerLink="/perfil"
        routerLinkActive="bottom-nav__item--active"
        aria-label="Perfil"
      >
        <svg
          class="bottom-nav__icon"
          width="24"
          height="24"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.75"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
          <circle cx="12" cy="7" r="4" />
        </svg>
        <span class="bottom-nav__label">Perfil</span>
      </a>
    </nav>
  `,
  styles: [
    `
      .bottom-nav {
        position: fixed;
        bottom: 0;
        left: 0;
        right: 0;
        height: calc(56px + env(safe-area-inset-bottom));
        padding-bottom: env(safe-area-inset-bottom);
        display: flex;
        background-color: var(--color-pergaminho-claro);
        border-top: 1px solid var(--color-borda-discreta);
        z-index: 100;
      }

      .bottom-nav__item {
        flex: 1;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 2px;
        color: var(--color-tinta-terciaria);
        text-decoration: none;
        -webkit-tap-highlight-color: transparent;
        transition: color var(--duration-fast) var(--easing-default);
      }

      .bottom-nav__item--active {
        color: var(--color-terracota);
      }

      .bottom-nav__icon {
        display: block;
        flex-shrink: 0;
      }

      .bottom-nav__label {
        font-family: var(--font-sans);
        font-size: 11px;
        font-weight: 500;
        letter-spacing: 0.01em;
      }
    `,
  ],
})
export class BottomNavComponent {}
