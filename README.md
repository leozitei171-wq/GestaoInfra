# Zitei - Sistema de Gestão de Infraestrutura

ASP.NET Core 9 MVC + EF Core + PostgreSQL 16.

## Rodar com Docker
```
copy .env.example .env   # ajuste as senhas
docker compose up -d --build
```
Acesse http://localhost:5080 (usuário inicial: `admin`; senha definida em `Seed__AdminPassword`).

## Testes
`dotnet test` (xUnit). E2E Playwright em `govtask-tests/` (`npm i` e `node verificar-*.js`).

## Aviso
Senhas padrão de desenvolvimento existem apenas para uso local. Troque antes de qualquer uso real.
