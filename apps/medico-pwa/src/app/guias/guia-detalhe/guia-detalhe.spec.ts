import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { Router, ActivatedRoute } from '@angular/router';
import { GuiaDetalheComponent } from './guia-detalhe';
import { MedicoGuiaService } from '../medico-guia.service';
import type { MedicoGuiaDetalheDto, MedicoItemGuiaDto } from '../medico-guia.types';

function makeItem(overrides: Partial<MedicoItemGuiaDto> = {}): MedicoItemGuiaDto {
  return {
    id: 'item-1',
    codigoTuss: '10101012',
    descricaoProcedimento: 'Consulta Médica',
    posicaoExecutor: 'Cirurgiao',
    valorApurado: 150.0,
    valorLiquidado: 120.0,
    situacaoCalculo: 'Calculado',
    ...overrides,
  };
}

function makeDetalhe(overrides: Partial<MedicoGuiaDetalheDto> = {}): MedicoGuiaDetalheDto {
  return {
    id: 'guia-1',
    operadoraNome: 'UNIMED JP',
    beneficiarioNome: 'João Silva',
    beneficiarioCarteira: '12345',
    dataAtendimento: '2026-01-15',
    numeroGuia: 'SEN001',
    situacao: 'Apresentada',
    observacao: null,
    itens: [makeItem()],
    ...overrides,
  };
}

function setup(
  detalhe: MedicoGuiaDetalheDto = makeDetalhe(),
  { deferred = false }: { deferred?: boolean } = {},
) {
  const subject = new Subject<MedicoGuiaDetalheDto>();
  const serviceSpy = {
    obterPorId: vi.fn().mockReturnValue(deferred ? subject.asObservable() : of(detalhe)),
  };
  const routerSpy = {
    navigate: vi.fn().mockReturnValue(Promise.resolve(true)),
  };
  const activatedRouteSpy = {
    snapshot: { paramMap: { get: vi.fn().mockReturnValue('guia-1') } },
  };

  TestBed.configureTestingModule({
    imports: [GuiaDetalheComponent],
    providers: [
      { provide: MedicoGuiaService, useValue: serviceSpy },
      { provide: Router, useValue: routerSpy },
      { provide: ActivatedRoute, useValue: activatedRouteSpy },
    ],
  });

  const fixture = TestBed.createComponent(GuiaDetalheComponent);
  fixture.detectChanges();

  return {
    fixture,
    component: fixture.componentInstance,
    service: serviceSpy,
    router: routerSpy,
    el: fixture.nativeElement as HTMLElement,
    subject,
  };
}

describe('GuiaDetalheComponent', () => {
  it('exibe nome do beneficiário e guia no cabeçalho', () => {
    const { el } = setup(makeDetalhe({ beneficiarioNome: 'Maria Souza', numeroGuia: 'SEN999' }));
    expect(el.textContent).toContain('Maria Souza');
    expect(el.textContent).toContain('SEN999');
  });

  it('bloco observação exibe texto quando preenchido', () => {
    const { el } = setup(makeDetalhe({ observacao: 'Guia com divergência na tabela.' }));
    const bloco = el.querySelector('.guia-detalhe__observacao');
    expect(bloco).not.toBeNull();
    expect(bloco?.textContent).toContain('Guia com divergência na tabela.');
  });

  it('bloco observação não é exibido quando observação é nula', () => {
    const { el } = setup(makeDetalhe({ observacao: null }));
    const bloco = el.querySelector('.guia-detalhe__observacao');
    expect(bloco).toBeNull();
  });

  it('exibe todos os itens da guia como cards', () => {
    const itens = [
      makeItem({ id: 'item-1', descricaoProcedimento: 'Consulta' }),
      makeItem({ id: 'item-2', descricaoProcedimento: 'Exame' }),
      makeItem({ id: 'item-3', descricaoProcedimento: 'Procedimento' }),
    ];
    const { el } = setup(makeDetalhe({ itens }));
    const cards = el.querySelectorAll('.guia-item');
    expect(cards).toHaveLength(3);
  });

  it('item Calculado exibe badge verde com valor apurado', () => {
    const itens = [makeItem({ situacaoCalculo: 'Calculado', valorApurado: 200.0 })];
    const { el } = setup(makeDetalhe({ itens }));
    const badge = el.querySelector('.badge--verde');
    expect(badge).not.toBeNull();
  });

  it('item SemTabela exibe badge ferrugem', () => {
    const itens = [makeItem({ situacaoCalculo: 'SemTabela' })];
    const { el } = setup(makeDetalhe({ itens }));
    const badge = el.querySelector('.badge--ferrugem');
    expect(badge).not.toBeNull();
  });

  it('item NaoCalculado exibe badge âmbar', () => {
    const itens = [makeItem({ situacaoCalculo: 'NaoCalculado' })];
    const { el } = setup(makeDetalhe({ itens }));
    const badge = el.querySelector('.badge--ambar');
    expect(badge).not.toBeNull();
  });

  it('botão voltar navega para /guias', () => {
    const { el, router } = setup();
    const btn = el.querySelector<HTMLButtonElement>('.guia-detalhe__voltar');
    btn?.click();
    expect(router.navigate).toHaveBeenCalledWith(['/guias']);
  });

  it('loading state exibido enquanto aguarda resposta', () => {
    const { el } = setup(makeDetalhe(), { deferred: true });
    const loading = el.querySelector('.guia-detalhe__loading');
    expect(loading).not.toBeNull();
  });
});
