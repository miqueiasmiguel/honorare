import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-saas-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="saas-layout">
      <nav class="saas-sidebar">
        <a class="saas-sidebar__link" routerLink="tenants" routerLinkActive="active">Tenants</a>
      </nav>
      <main class="saas-content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      .saas-layout {
        display: flex;
        height: 100%;
      }

      .saas-sidebar {
        width: 240px;
        padding: 1rem;
        background-color: #1a1a2e;
      }

      .saas-sidebar__link {
        display: block;
        padding: 0.5rem 1rem;
        color: #e0e0e0;
        text-decoration: none;
        border-radius: 4px;
      }

      .saas-sidebar__link.active {
        background-color: #16213e;
        color: #fff;
      }

      .saas-content {
        flex: 1;
        padding: 1.5rem;
        overflow-y: auto;
      }
    `,
  ],
})
export class SaasShell {}
