# RemoteAccess

Sistema de acesso remoto estilo AnyDesk, desenvolvido em .NET 8.

## Arquitetura

```
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│  Host (PC A)  │◄──WS──►│  Relay Server │◄──WS──►│ Viewer (PC B) │
│  Compartilha  │         │  Faz a ponte  │         │  Controla     │
│  a tela       │         │  entre os     │         │  remotamente  │
└──────────────┘         │  dois         │         └──────────────┘
                         └──────────────┘
```

### Projetos

| Projeto | Descrição |
|---------|-----------|
| **RemoteAccess.Shared** | Protocolo de comunicação (mensagens JSON + frames binários) |
| **RemoteAccess.Relay** | Servidor relay WebSocket (ASP.NET Core) — faz a ponte entre host e viewer |
| **RemoteAccess.Desktop** | App Windows (WinForms) — funciona como host E viewer simultaneamente |

## Como usar

### 1. Iniciar o Relay Server

```bash
cd RemoteAccess.Relay
dotnet run
```

O relay inicia em `ws://localhost:5050/ws`. Para uso na internet, hospede em um VPS com IP público.

### 2. Executar o App Desktop

```bash
cd RemoteAccess.Desktop
dotnet run
```

Ao abrir, o app:
- Gera um **ID de 9 dígitos** (ex: `482 731 095`)
- Gera uma **senha aleatória** de 6 caracteres
- Se conecta automaticamente ao relay como host

### 3. Conectar remotamente

Na outra máquina (com o mesmo app aberto):
1. Digite o **ID** da máquina destino
2. Digite a **senha** exibida na máquina destino
3. Clique em **Conectar**

Uma janela de visualização abrirá mostrando a tela remota com controle total de mouse e teclado.

## Funcionalidades

- **Código de acesso** — ID numérico de 9 dígitos gerado automaticamente
- **Senha** — proteção por senha com possibilidade de regenerar
- **Streaming de tela** — captura e compressão JPEG em tempo real (~12 FPS)
- **Controle total** — mouse (mover, clicar, scroll) + teclado completo
- **Cursor visível** — renderiza o cursor do host na captura
- **Tela cheia** — F11 ou botão para visualização em tela cheia (ESC para sair)
- **Qualidade ajustável** — Baixa/Média/Alta
- **Reconexão automática** — se o relay cair, o host reconecta sozinho
- **Dark theme** — interface moderna com tema escuro

## Configuração do Relay

### Para uso local (mesma rede)
O padrão `ws://localhost:5050` funciona se ambas as máquinas estão na mesma rede (troque `localhost` pelo IP da máquina do relay).

### Para uso pela internet
1. Hospede o relay em um VPS (DigitalOcean, AWS, etc.)
2. Configure um domínio com SSL (recomendado):
   ```
   wss://relay.seudominio.com/ws
   ```
3. No app desktop, altere o endereço do relay no campo inferior

### Deploy do Relay com Docker (opcional)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "RemoteAccess.Relay.dll"]
EXPOSE 5050
```

```bash
cd RemoteAccess.Relay
dotnet publish -c Release -o publish
docker build -t remote-relay .
docker run -d -p 5050:5050 remote-relay
```

## Build para distribuição

```bash
# Relay
cd RemoteAccess.Relay
dotnet publish -c Release -r linux-x64 --self-contained -o dist/relay

# Desktop (Windows)
cd RemoteAccess.Desktop
dotnet publish -c Release -r win-x64 --self-contained -o dist/desktop
```

O app desktop será um executável único para distribuir aos clientes.

## Protocolo

- **Texto (JSON)**: mensagens de controle (register, connect, input, screen_info)
- **Binário**: frames JPEG da tela

## Requisitos

- .NET 8 SDK
- Windows (app desktop — usa APIs nativas para captura e input)
- Relay pode rodar em Windows ou Linux
