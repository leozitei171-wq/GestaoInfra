# Planejamento — Sistema de Gestão de Apontamentos e Custos (Infraestrutura Urbana)

> Documento gerado em: 27/08/2026
> Baseado na análise do sistema GovTask (conectagov.com.br)
> 32 páginas internas documentadas e analisadas

---

## 1. Visão Geral

Sistema web multi-tenant para gestão de demandas de infraestrutura urbana em prefeituras. Controla o ciclo completo: abertura de demandas → programação → execução → medição → rateio de custos, com transparência pública e geolocalização.

### 1.1 Objetivos
- Registrar e rastrear atividades de manutenção urbana (capina, coleta, tapa-buraco, poda, etc.)
- Programar e acompanhar execução com controle de prazos
- Calcular e ratear custos por contrato/serviço
- Prover transparência pública (LAI) sem expor dados pessoais
- Geolocalizar demandas em mapa interativo
- Gerar indicadores de desempenho operacional

---

## 2. Perfis de Usuário (RBAC)

| Perfil | Permissões principais |
|--------|----------------------|
| **admin** | Acesso total. Gerencia tenants, usuários, RBAC, wizard por perfil |
| **gestor** | Gerencia atividades, programação, contratos, indicadores, rateios |
| **servidor** | Registra e executa atividades. Acesso ao operacional |
| **vereador** | Visualiza demandas da sua região/distrito (somente leitura) |
| **cidadao** | Registra demandas. Consulta status das suas solicitações |

### 2.1 Configuração por perfil (Wizard Config)
- Cada perfil tem um wizard de criação de atividade configurável:
  - Quais passos estão habilitados (Onde / Detalhes / Evidência)
  - Quais campos são obrigatórios (tipo, local, prioridade, descrição, foto)
  - Passo inicial

---

## 3. Módulos Funcionais

### 3.1 Operacional

#### 3.1.1 Dashboard
- Resumo de atividades por status
- Cards com totais e percentuais
- Atalhos rápidos para ações principais

#### 3.1.2 Atividades (CRUD + Listagem)
**Listagem com colunas:**
- ID, Descrição, Tipo, Setor, Status, Prioridade, Progresso (%), Foto (ícone), Abertura, Aberto por

**Criação (Wizard 3 passos):**

| Passo | Campos |
|-------|--------|
| 1 — Onde | Tipo de atividade (select com setor automático), Endereço/CEP (select2 com busca: Bairro + Logradouro + CEP), Rua (auto), Número, Latitude, Longitude (manual ou via mapa), Local relacionado (opcional) |
| 2 — Detalhes | Prioridade (select), Descrição (textarea), Requerimento (checkbox + nº + arquivo PDF/JPEG/PNG até 15MB) |
| 3 — Evidência | Foto (JPEG/PNG até 5MB, obrigatoriedade configurável por perfil) |

#### 3.1.3 Programação
- Filtros: Período (semana/mês/quinzena), Data de referência, Status
- Lista de atividades programadas para o período
- Gestão de datas previstas de início/fim

#### 3.1.4 Mapa de Atividades
- Mapa Leaflet com tiles OpenStreetMap
- CircleMarkers coloridos por status:
  - 🟣 `#AE6BFF` — ABERTA
  - 🩷 `#EE6BFF` — PROGRAMADA
  - 🟡 `#E6FF6B` — EM EXECUÇÃO
  - 🔴 `#FF6B6B` — ATRASADA
  - ⚫ `#767184` — NEGADA
  - 🟢 `#049A15` — CONCLUÍDA
- Filtro por bairro (Select2) e período (Datepicker)
- Popup no marker com: local, endereço, bairro, CEP, status
- Auto-refresh com countdown
- Busca no mapa por endereço

#### 3.1.5 Consulta Pública (Transparência)
- Acesso sem login
- Exibe: Ref, Descrição, Tipo, Setor, Local/região, Situação, Progresso (%), Data registro
- SEM dados pessoais (quem abriu)
- SEM fotos (restritas ao acesso interno)
- Filtro por prefeitura e região/bairro
- Paginação (20/página)
- Detalhe público com: descrição, tipo, setor, situação, prioridade, progresso, datas

---

### 3.2 Gestão

#### 3.2.1 Indicadores (Dashboard analítico)
5 painéis com gráficos ApexCharts:
1. **Capacidade operacional** — atividades por período vs capacidade
2. **Tempo de execução** — média de dias entre abertura e conclusão
3. **Uso de contratos** — percentual de utilização dos contratos ativos
4. **Demandas por setor** — distribuição entre departamentos
5. **Demandas por tipo de atividade** — tipos mais demandados

#### 3.2.2 Alertas de Contratos
- Contratos próximos do vencimento
- Contratos com limite de valor próximo de atingir
- Painel de avisos automáticos

#### 3.2.3 Auditoria
- Log de ações dos usuários
- Rastreabilidade de alterações

#### 3.2.4 Rateio Proporcional
- Distribuição proporcional de custos indiretos
- Campos: Valor total a ratear (R$), Descrição/Motivo, Data do rateio
- Cálculo automático de distribuição por critérios configuráveis

#### 3.2.5 Fechamento Mensal (planejado)
- Consolidação mensal de custos por contrato/setor

---

### 3.3 Cadastros Base

| Cadastro | Campos principais |
|----------|-------------------|
| **Tipos de Medição** | Código, descrição, unidade |
| **Setores** | Código hierárquico (ex: INFRA_2132_SLU), descrição |
| **Tipos de Atividade** | Código, descrição, setor vinculado |
| **Tipos de Serviço** | Código, descrição |
| **Atividade × Serviços** | Vínculo N:N entre tipos de atividade e serviços |
| **Fornecedores** | Razão social, CNPJ, contato |
| **Contratos** | Código, descrição, fornecedor, setor, PDF, vigência (início/fim), serviços (N itens com tipo_medição + quantidade + unitário_R$ + limite), valor total |
| **Status de Atividade** | Código (ABERTA, PROG, EM_EXEC, ATRASADA, NEGADA, CONCL), descrição, cor |
| **Tipos de Mão de Obra** | Código, descrição |
| **Funções do Servidor** | Código, descrição |
| **Prioridades** | Código (URG, NORM), descrição |
| **Tipos de Custo** | Código, descrição |
| **Tipos de Rateio** | Código, descrição, regra de distribuição |
| **Regiões** | Código, nome (ex: REGIAO CENTRAL, REGIAO SUL) |
| **Sub-regiões** | Código, nome, região_pai |
| **Bairros** | Nome, sub-região |
| **Logradouros** | Nome, tipo (Rua/Av/Pça), CEP, bairro |
| **Locais** | Descrição, logradouro, bairro, número, lat, lng, CEP |

---

### 3.4 Administração

| Funcionalidade | Descrição |
|---------------|-----------|
| **Prefeituras (Tenants)** | Multi-tenant: cada prefeitura é isolada |
| **Usuários e Perfis** | CRUD: nome, login, email, prefeitura, perfil, senha, ativo |
| **Permissões RBAC** | Matriz de permissões por perfil/recurso |
| **Wizard por Perfil** | Configuração do formulário de nova atividade por perfil |
| **Importar GeoJSON** | Importação de dados geográficos (planejado) |

---

## 4. Modelo de Dados (Entidades Principais)

```
┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│   Prefeitura     │       │     Usuário       │       │   Permissão      │
│ (Tenant)         │1────N │                   │N────N │   (RBAC)         │
├──────────────────┤       ├──────────────────┤       ├──────────────────┤
│ id               │       │ id               │       │ id               │
│ nome             │       │ nome             │       │ perfil           │
│ cidade           │       │ login            │       │ recurso          │
│ uf               │       │ email            │       │ acao             │
│ ativo            │       │ senha_hash       │       │ permitido        │
└──────────────────┘       │ perfil           │       └──────────────────┘
                           │ id_prefeitura(FK)│
                           │ ativo            │
                           └──────────────────┘

┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│   Atividade      │       │   Contrato       │       │  Fornecedor      │
│ (Demanda)        │       │                  │N────1 │                  │
├──────────────────┤       ├──────────────────┤       ├──────────────────┤
│ id               │       │ id               │       │ id               │
│ descricao        │       │ codigo           │       │ razao_social     │
│ id_tipo_ativ(FK) │       │ descricao        │       │ cnpj             │
│ id_setor(FK)     │       │ id_fornecedor(FK)│       │ contato          │
│ id_prioridade(FK)│       │ id_setor(FK)     │       └──────────────────┘
│ id_status(FK)    │       │ arquivo_pdf      │
│ id_prefeitura(FK)│       │ valor_total      │
│ id_usuario_ab(FK)│       │ id_prefeitura(FK)│
│ rua              │       └──────────────────┘
│ numero           │              │1
│ latitude         │              │
│ longitude        │              N
│ id_logradouro(FK)│       ┌──────────────────┐
│ id_bairro(FK)    │       │ Contrato_Vigencia│
│ id_local(FK)     │       ├──────────────────┤
│ progresso        │       │ id               │
│ foto_path        │       │ id_contrato(FK)  │
│ requerimento_num │       │ data_inicio      │
│ requerimento_arq │       │ data_fim         │
│ data_abertura    │       └──────────────────┘
│ data_inicio_prev │              │1
│ data_fim_prev    │              │
│ data_conclusao   │              N
└──────────────────┘       ┌──────────────────┐
                           │Contrato_Servico  │
                           ├──────────────────┤
                           │ id               │
                           │ id_vigencia(FK)  │
                           │ id_tipo_serv(FK) │
                           │ id_tipo_med(FK)  │
                           │ quantidade       │
                           │ valor_unitario   │
                           │ limite           │
                           └──────────────────┘

┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│   Região         │       │   Sub-Região     │       │     Bairro       │
├──────────────────┤1────N ├──────────────────┤1────N ├──────────────────┤
│ id               │       │ id               │       │ id               │
│ codigo           │       │ codigo           │       │ nome             │
│ nome             │       │ nome             │       │ id_sub_regiao(FK)│
│ id_prefeitura(FK)│       │ id_regiao(FK)    │       └──────────────────┘
└──────────────────┘       └──────────────────┘              │1
                                                             │N
                                                      ┌──────────────────┐
                                                      │   Logradouro     │
                                                      ├──────────────────┤
                                                      │ id               │
                                                      │ nome             │
                                                      │ tipo             │
                                                      │ cep              │
                                                      │ id_bairro(FK)    │
                                                      └──────────────────┘

┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│    Rateio        │       │  Rateio_Item     │       │  Auditoria       │
├──────────────────┤1────N ├──────────────────┤       ├──────────────────┤
│ id               │       │ id               │       │ id               │
│ valor_total      │       │ id_rateio(FK)    │       │ id_usuario(FK)   │
│ descricao        │       │ id_contrato(FK)  │       │ acao             │
│ data_rateio      │       │ valor_rateado    │       │ entidade         │
│ id_prefeitura(FK)│       │ percentual       │       │ id_entidade      │
└──────────────────┘       └──────────────────┘       │ dados_antes      │
                                                      │ dados_depois     │
                                                      │ created_at       │
                                                      └──────────────────┘
```

---

## 5. Fluxo Principal (Ciclo de Vida da Atividade)

```
CIDADÃO/SERVIDOR                    GESTOR                         SISTEMA
      │                                │                               │
      │── Registra demanda ──────────►│                               │
      │   (wizard 3 passos)            │                               │
      │                                │                               │
      │                                │◄── Status: ABERTA             │
      │                                │                               │
      │                                │── Programa execução ────────►│
      │                                │   (data_inicio, data_fim)     │
      │                                │                               │
      │                                │◄── Status: PROGRAMADA         │
      │                                │                               │
      │                                │── Atribui equipe ───────────►│
      │                                │                               │
      │                                │◄── Status: EM EXECUÇÃO        │
      │                                │                               │
      │                                │── Registra progresso (%) ───►│
      │                                │                               │
      │                                │── Finaliza ─────────────────►│
      │                                │                               │
      │                                │◄── Status: CONCLUÍDA          │
      │                                │                               │
      │                                │    [se data_fim < hoje e      │
      │                                │     não concluída]            │
      │                                │◄── Status: ATRASADA (auto)    │
      │                                │                               │
      │◄── Consulta transparência ────│                               │
```

---

## 6. Stack Tecnológica Sugerida

Considerando que o time trabalha com C# (conforme padrões locais), e o sistema referência usa uma arquitetura server-side:

### 6.1 Backend
| Camada | Tecnologia | Justificativa |
|--------|-----------|---------------|
| Framework | **ASP.NET Core 8 (MVC ou Razor Pages)** | Alinhado com padrões do time, server-side como referência |
| ORM | **Entity Framework Core 8** | Migrations, relacionamentos, multi-tenant |
| Autenticação | **ASP.NET Identity** | RBAC built-in, login/senha, recuperação |
| API (futuro) | **Minimal APIs** | Para mobile/SPA futuramente |

### 6.2 Frontend
| Camada | Tecnologia | Justificativa |
|--------|-----------|---------------|
| Template | **Bootstrap 5** | Mesmo padrão da referência |
| Mapas | **Leaflet.js + OpenStreetMap** | Open source, zero custo, comprovado |
| Gráficos | **ApexCharts** | Open source, mesmo da referência |
| Selects | **Select2** ou **Tom Select** | Busca em grandes listas (logradouros) |
| Datepicker | **Flatpickr** | Leve, moderno, bom suporte pt-BR |

### 6.3 Infraestrutura
| Camada | Tecnologia |
|--------|-----------|
| Banco de dados | **PostgreSQL 16** ou **SQL Server** |
| Storage (fotos/PDFs) | **Azure Blob** ou filesystem local |
| Deploy | **Docker + Docker Compose** ou **Azure App Service** |
| CI/CD | **GitHub Actions** |

### 6.4 Alternativa SPA (se preferir)
| Camada | Tecnologia |
|--------|-----------|
| Frontend | **React** ou **Vue 3** + Vite |
| Backend | **ASP.NET Core Web API** |
| Auth | **JWT + Refresh Token** |

---

## 7. Arquitetura Multi-Tenant

```
┌─────────────────────────────────────────────────────┐
│                    Aplicação                          │
├─────────────────────────────────────────────────────┤
│  Tenant Resolver (por header, subdomain ou claim)    │
├─────────────────────────────────────────────────────┤
│  Query Filter Global (WHERE prefeitura_id = @tenant) │
├─────────────────────────────────────────────────────┤
│  Banco de Dados Compartilhado (schema único)         │
│  Todas as tabelas principais têm id_prefeitura       │
└─────────────────────────────────────────────────────┘
```

**Estratégia recomendada:** Schema compartilhado com filtro global no EF Core.
- Menor custo de infraestrutura
- Simples de manter
- Isolamento via query filter automático

---

## 8. Estrutura de Projeto Sugerida

```
SistemaGestaoInfra/
├── src/
│   ├── SistemaGestaoInfra.Web/          # Projeto MVC (UI + Controllers)
│   │   ├── Controllers/
│   │   ├── Views/
│   │   ├── wwwroot/                     # Assets (CSS, JS, Leaflet, ApexCharts)
│   │   └── Program.cs
│   ├── SistemaGestaoInfra.Domain/       # Entidades, enums, interfaces
│   ├── SistemaGestaoInfra.Application/  # Use cases, DTOs, serviços
│   └── SistemaGestaoInfra.Infra/        # EF Core, Repos, Storage
├── tests/
│   ├── Unit/
│   └── Integration/
├── docs/
│   └── PLANEJAMENTO.md                  # Este documento
├── docker-compose.yml
└── SistemaGestaoInfra.sln
```

---

## 9. Fases de Implementação (MVP → Completo)

### Fase 1 — MVP (4-6 semanas)
- [ ] Setup projeto (ASP.NET Core + EF Core + PostgreSQL)
- [ ] Autenticação (login, logout, recuperar senha)
- [ ] CRUD Usuários + Perfis (admin/gestor/servidor)
- [ ] Cadastros base: Setores, Tipos de Atividade, Prioridades, Status
- [ ] CRUD Atividades (wizard simplificado)
- [ ] Listagem de atividades com filtros
- [ ] Upload de fotos

### Fase 2 — Geolocalização + Programação (3-4 semanas)
- [ ] Cadastro: Regiões, Sub-regiões, Bairros, Logradouros, Locais
- [ ] Mapa Leaflet com circleMarkers por status
- [ ] Busca de endereço no wizard (select2 com logradouros)
- [ ] Picker de coordenadas no mapa
- [ ] Programação (agenda com filtros de período/status)
- [ ] Motor de status automático (ATRASADA quando passa da data)

### Fase 3 — Contratos e Custos (3-4 semanas)
- [ ] CRUD Fornecedores
- [ ] CRUD Contratos (vigências + serviços com medição/quantidade/valor)
- [ ] Tipos de Serviço, Tipos de Medição, Tipos de Custo
- [ ] Rateio proporcional
- [ ] Alertas de contratos (vencimento + limite)

### Fase 4 — Gestão e Transparência (2-3 semanas)
- [ ] Dashboard com indicadores (ApexCharts)
- [ ] Portal de transparência (público, sem login)
- [ ] Auditoria (log de ações)
- [ ] Relatórios/exportações

### Fase 5 — Admin Avançado (2 semanas)
- [ ] Multi-tenant completo (prefeituras)
- [ ] RBAC granular (permissões por recurso)
- [ ] Wizard configurável por perfil
- [ ] Importação GeoJSON
- [ ] Perfil cidadão e vereador

---

## 10. Requisitos Não-Funcionais

| Requisito | Meta |
|-----------|------|
| **Performance** | Listagens < 500ms, mapa < 2s para 1000 markers |
| **Segurança** | CSRF token, senha hash (bcrypt/argon2), RBAC em cada endpoint |
| **Responsividade** | Mobile-first (formulário de nova atividade usado em campo) |
| **Acessibilidade** | WCAG 2.1 AA mínimo |
| **Multi-tenant** | Isolamento total de dados entre prefeituras |
| **Upload** | Fotos até 5MB, PDFs até 15MB, validação server-side |
| **Auditoria** | Log de todas as operações de escrita |
| **Disponibilidade** | 99.5% (considerar HA em produção) |

---

## 11. Integrações Possíveis (futuro)

- **ViaCEP/BrasilAPI** — busca de endereço por CEP
- **IBGE** — dados de municípios/estados
- **OpenStreetMap Nominatim** — geocoding reverso
- **Email (SMTP)** — notificações, recuperação de senha
- **Push notifications** — alertas de atrasados para gestores
- **Power BI / Metabase** — BI avançado conectando no banco

---

## 12. Referência de Cores/Status

```css
:root {
  --status-aberta:    #AE6BFF;  /* Roxo      */
  --status-prog:      #EE6BFF;  /* Rosa      */
  --status-em-exec:   #E6FF6B;  /* Amarelo   */
  --status-atrasada:  #FF6B6B;  /* Vermelho  */
  --status-negada:    #767184;  /* Cinza     */
  --status-concluida: #049A15;  /* Verde     */
}
```

---

## 13. Decisões em Aberto

| # | Decisão | Opções | Impacto |
|---|---------|--------|---------|
| 1 | Arquitetura frontend | MVC server-side vs SPA (React/Vue) | Complexidade, UX, time necessário |
| 2 | Banco de dados | PostgreSQL vs SQL Server | Custo, familiaridade do time |
| 3 | Hosting | Azure vs AWS vs on-premises | Custo, compliance |
| 4 | Storage de arquivos | Blob cloud vs filesystem local | Escalabilidade, backup |
| 5 | Mobile | PWA vs app nativo vs responsive-only | Uso em campo pelos servidores |

---

*Próximo passo: definir as decisões em aberto e iniciar a Fase 1 (MVP).*
