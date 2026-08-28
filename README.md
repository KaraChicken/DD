# DDTank - Versão 4.1

> Este projeto é uma base open source para desenvolvimento de um servidor DDTank.

## Estado do projeto

O projeto está em fase de desenvolvimento e não está 100% concluído.

## Estrutura e direção da reconstrução

A reconstrução moderna do projeto será desenvolvida principalmente dentro do diretório **`app/`**.

### `app/` é o diretório raiz da reconstrução

Novos componentes e serviços devem ser organizados dentro de `app/`, mantendo o código legado existente separado e preservado como referência.

A arquitetura planejada é:

```text
app/
├── web/        # Vue: página inicial, login, registro e Game Shell
├── api/        # Node.js: backend e regras do jogo
├── db/         # SQLite e camada de persistência
└── flash/      # Protocolo e integração com o cliente Flash
```

### Cliente Flash

O **Flash é o cliente principal do jogo**. Depois do login, a área central da página será ocupada pelo programa Flash/SWF, que controla a interface e todas as ações dentro do jogo.

Portanto, os botões e telas do jogo não serão reimplementados em Vue. As ações realizadas dentro do Flash devem se comunicar diretamente com o backend Node.js através do protocolo utilizado pelo cliente Flash.

O diretório **`Source Flash/`** é mantido como referência principal para compreender o fluxo de execução, chamadas ao servidor, formato dos pacotes e comportamento do cliente.

### Backend

O backend da reconstrução será baseado principalmente em **Node.js/JavaScript**, com **SQLite** permitido como banco de dados inicial.

O objetivo é substituir gradualmente o servidor legado mantendo a compatibilidade necessária com o cliente Flash.

Fluxo principal:

```text
Vue
 ├── Home
 ├── Login
 └── Register
        │
        ▼
   Game Shell
        │
        ▼
   Flash / SWF
        │
        │ Game Protocol
        ▼
     Node.js
        │
        ▼
      SQLite
```

## Build legado

Para compilar a versão legada do projeto, utilize o Visual Studio 2019 com o .NET Framework 4.5.2.

## Configurando

- https://www.youtube.com/watch?v=zYMC9TeS3Q4

## Info +

- Acesse a [wiki](https://github.com/SkelletonX/DDTank4.1/wiki)

## Membros do Projeto

- **SKELLETONX**
- amrmostafa800

## Discord

- https://discord.com/invite/4BRuPVV6bq

## Release Server

- https://github.com/SkelletonX/DDTank4.1/releases

<p align="center">
  <img src="https://i.imgur.com/JQ8Ssdb.jpg"/>
</p>
