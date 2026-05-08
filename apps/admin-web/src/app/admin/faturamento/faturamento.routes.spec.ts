import { TestBed } from '@angular/core/testing';
import { provideRouter, type Routes } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { CatalogService } from '../catalog/catalog.service';
import { GuiaService } from './guia.service';
import { faturamentoRoutes } from './faturamento.routes';
import { GuiaFormComponent } from './guia-form/guia-form.component';
import { GuiaListComponent } from './guia-list/guia-list.component';

const testRoutes: Routes = [{ path: 'guias', children: faturamentoRoutes }];

describe('faturamentoRoutes', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      // Pre-import to trigger compileComponents() and resolve external templateUrl/styleUrl
      imports: [GuiaListComponent, GuiaFormComponent],
      providers: [
        provideRouter(testRoutes),
        {
          provide: GuiaService,
          useValue: {
            listar: vi
              .fn()
              .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 20 })),
            obterPorId: vi.fn().mockReturnValue(of(null)),
            excluir: vi.fn().mockReturnValue(of(undefined)),
          },
        },
        {
          provide: CatalogService,
          useValue: {
            listarPrestadores: vi
              .fn()
              .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
            listarOperadoras: vi
              .fn()
              .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 200 })),
            listarProcedimentos: vi
              .fn()
              .mockReturnValue(of({ itens: [], total: 0, pagina: 1, itensPorPagina: 10 })),
          },
        },
      ],
    }).compileComponents();
  });

  it('rota /guias resolve para GuiaListComponent', async () => {
    const harness = await RouterTestingHarness.create('/guias');
    const el = harness.fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-guia-list')).not.toBeNull();
  });

  it('rota /guias/nova resolve para GuiaFormComponent', async () => {
    const harness = await RouterTestingHarness.create('/guias/nova');
    const el = harness.fixture.nativeElement as HTMLElement;
    expect(el.querySelector('app-guia-form')).not.toBeNull();
  });
});
