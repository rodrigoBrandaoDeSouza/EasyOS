# EasyOS

Sistema de ordens de serviço para empresas de controle de pragas. **Uma única aplicação** (backend + frontend) atende todos os clientes da EasyOS: cada cliente acessa pelo próprio domínio e continua com o **próprio banco de dados**.

> O sistema antigo (Web API 2 + WebForms, um deploy por cliente) está em `old/` apenas como referência e **não deve ser versionado** — os `Web.config` têm senhas em texto puro.

## Arquitetura

```
 os.cliente-a.com.br ─┐                         ┌─► banco do cliente A (SQL Server/Azure)
 os.cliente-b.com.br ─┼─► Caddy (HTTPS) ─► API ─┼─► banco do cliente B
 admin.easyos.com.br ─┘     .NET 10 + React     ├─► catálogo (SQLite): clientes, domínios, configurações
                                                └─► storage S3 (R2/B2): arquivos, logos, assinaturas
```

- **Resolução do cliente pelo domínio**: cada requisição tem o host procurado no catálogo (`TenantDomains`, com cache). O `TenantDbContext` usa a connection string desse cliente, só naquela requisição. Domínio não cadastrado recebe 404.
- **Isolamento**: a sessão (cookie) é emitida para o domínio do cliente e carrega o id do tenant; um cookie apresentado em outro domínio é rejeitado. Arquivos ficam em `tenants/{identificador}/…` no storage.
- **Bancos dos clientes sem alteração de esquema**: o app mobile continua usando a API antiga sobre os mesmos bancos durante a migração. Por isso o formato de senha (MD5) e as convenções de data (`offset_date`) do legado foram mantidos.
- **Painel EasyOS** (`admin.…`): cadastro de clientes, domínios, connection string (cifrada), identidade visual, SMTP e as configurações que antes ficavam no `Web.config` de cada cliente (`SEQUENCE_BY_COSTUMER`, `CERTIFICATEDATEONVISIT`, `DayVisitsOnly`, flags `CFG_*`…).

| Pasta | Conteúdo |
|---|---|
| `src/backend/EasyOS.Domain` | Entidades mapeadas no esquema legado e modelo do catálogo |
| `src/backend/EasyOS.Infrastructure` | EF Core (tenant e catálogo), multi-tenancy, storage S3/local, e-mail, geocodificação |
| `src/backend/EasyOS.Api` | Endpoints por área (`Features/*`), autenticação, painel EasyOS |
| `src/backend/tests/EasyOS.Tests` | Testes de integração (dois tenants em SQLite) |
| `src/frontend` | React + TypeScript + Mantine (Vite). `public/inea/` tem os formulários oficiais do INEA |
| `deploy/` | Dockerfile, docker-compose (app + Caddy) e `.env.example` |

## Desenvolvimento

Pré-requisitos: .NET SDK 10, Node 24, um SQL Server com um banco de cliente (pode ser um container).

```bash
# API (http://localhost:5080). O catálogo é criado em src/backend/EasyOS.Api/data/
cd src/backend/EasyOS.Api
Platform__BootstrapAdmin__Email=voce@easyos.com.br Platform__BootstrapAdmin__Password='senha-forte-123' \
  ASPNETCORE_URLS=http://localhost:5080 dotnet run

# Frontend (http://localhost:5173, com proxy para a API)
cd src/frontend && npm install && npm run dev
```

1. Acesse `http://admin.localhost:5173` (painel), entre com o usuário inicial e cadastre um cliente com o domínio `localhost` e a connection string do banco de teste. Use **Testar conexão**: ela também confere se o banco tem todas as tabelas e colunas esperadas.
2. Acesse `http://localhost:5173` e entre com um usuário da empresa ou do cliente daquele banco.

Testes: `cd src/backend && dotnet test` · Typecheck/build do frontend: `cd src/frontend && npx tsc -b && npx vite build`.

## Implantação (VPS única)

Custo estimado: VPS 2 vCPU/4 GB (~US$ 6–15/mês) + Cloudflare R2 (10 GB grátis) + bancos que já existem no Azure SQL.

1. VPS Linux com Docker. Libere as portas 80 e 443.
2. Crie o bucket no R2 (ou B2) e uma chave de acesso.
3. `cd deploy && cp .env.example .env` e preencha (storage, SMTP padrão, domínio do painel, usuário inicial).
4. `docker compose up -d --build`.
5. Aponte o DNS do domínio do painel para a VPS e acesse-o. O certificado HTTPS é emitido automaticamente.
6. **Libere o IP da VPS no firewall do Azure SQL** de cada servidor de banco.
7. **Backup**: o volume `easyos-data` contém o catálogo e as chaves que cifram as connection strings e as sessões. Sem as chaves, as connection strings não podem ser lidas. Faça backup diário (ex.: `sqlite3 catalog.db ".backup …"` + pasta `keys`, enviados ao R2).

## Migração de um cliente (um por vez)

1. **Painel → Novo cliente**: identificador (ex.: `termite`), nome, connection string do banco atual → **Testar conexão** (precisa dar OK, inclusive o esquema).
2. **Identidade visual**: cor (está no `clientsConfig.yaml` do sistema antigo), rótulo do 5º inseto (`th6`: "Outros" ou "Abelhas"), e envio de logo, fundo do login, favicon e logo dos PDFs (arquivos de `assets/img` do deploy do cliente).
3. **Regras**: copie do `Web.config` da API do cliente: `SEQUENCE_BY_COSTUMER`, `CERTIFICATEDATEONVISIT`, `DayVisitsOnly`, `TravelTime`, `LockTime`, chaves `CFG_*` e SMTP.
4. **Arquivos**: copie a pasta `customer_files` do servidor do cliente para `tenants/{identificador}/customer_files/` no bucket, por exemplo com `rclone copy ./customer_files r2:easyos-files/tenants/termite/customer_files`.
5. **Assinaturas**: no sistema novo, em *Configurações → Responsáveis técnicos*, envie `assinatura-{uf}.png` de cada UF. Se o cliente usar o RAAE, envie também `assinatura-representante-legal.png` (aba INEA).
6. Adicione no painel um **domínio provisório** (ex.: `termite.easyos.com.br`) e valide com o cliente.
7. **Virada**: inclua o domínio definitivo no painel e altere o DNS dele para a VPS. O app mobile continua apontando para a API antiga do cliente até a migração do app.
8. Depois de alguns dias estável, desligue o site WebForms antigo daquele cliente (mantenha a API antiga enquanto o app depender dela).

## Diferenças em relação ao sistema antigo

Funcionamento mantido, com correções de segurança e de bugs:

- **Removido** o endpoint `Company/cleanDatabase`, que apagava todas as tabelas sem autenticação.
- Sessão por cookie seguro (HttpOnly, SameSite=Strict), revalidada a cada 5 minutos: usuário desativado ou senha trocada encerra a sessão. Antes, o token era `MD5(email:senha)`, fixo e sem expiração.
- Rate limit no login, proteção CSRF e controle de escopo em todas as consultas: usuário de cliente só acessa o próprio cliente; usuário da empresa só acessa clientes da sua empresa.
- Geocodificação feita no servidor. Antes, a chave do Google Maps ficava exposta no HTML.
- Datas: o último dia do período agora entra nos filtros. O check-in manual pela web usa o mesmo referencial do app (antes aparecia 3 h deslocado).
- Editar uma visita pela web não apaga mais os técnicos auxiliares nem as áreas tratadas vindos do app.
- "Downloads" passa a ter envio, exclusão e criação de pastas pelo administrador (antes era por FTP).
- Sem integração ContaAzul/Locaweb (fora do escopo).

## Pendências conhecidas

- **App mobile**: segue na API antiga. Para migrá-lo, será preciso:
  - endpoints equivalentes aos usados pelo app (`login`, check-in, confirmação, envio de OS, `notifications/send_email`);
  - geração dos PDFs de OS, certificado e não conformidades enviados por e-mail (`OsPdfCreator` e afins em `old/`);
  - descobrir o cliente sem depender da URL por cliente.
- **Senhas**: continuam em MD5 por compatibilidade com o app. Quando o app migrar, trocar por hash forte, com rehash transparente no login.
