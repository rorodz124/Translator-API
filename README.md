# Translator API

![.NET](https://img.shields.io/badge/.NET-9.0-blue)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-API-lightgrey)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED)

API HTTP para tradução de texto, construída sobre o motor de tradução [LibreTranslate](https://libretranslate.com/). Permite traduzir texto para vários idiomas em simultâneo, guardar o histórico das traduções e consultar/descarregar registos anteriores. Inclui também uma interface web simples (`wwwroot/index.html`) para usar a API sem precisar de chamadas diretas.

O projeto corre em dois contentores: o **LibreTranslate** (motor de tradução) e a **Translator API** (wrapper), ligados em rede interna via Docker Compose.

## Como Iniciar

Clone o repositório e entra na pasta do projeto.

Inicia com Docker:

```bash
docker compose up --build -d
```

A aplicação (incluindo a interface web) fica disponível em **http://localhost:5100**

A Swagger UI fica disponível em **http://localhost:5100/swagger**

## Configuração

As variáveis de configuração são definidas no `docker-compose.yml` ou em `appsettings.json`:

| Variável | Descrição | Valor por defeito |
|----------|-----------|-------------------|
| `LibreTranslate__Url` | URL base do serviço LibreTranslate | `http://localhost:5050` |
| `Storage__HistoricoFolder` | Pasta onde são guardados os registos de tradução (JSON) | `JSON history` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente da aplicação | `Production` |


## API Endpoints

### Idiomas

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `GET` | `/api/languages` | Lista os idiomas suportados pelo LibreTranslate |

#### `GET /api/languages?refresh=true`

O parâmetro `refresh` (opcional, `bool`) força a atualização da lista de idiomas em cache.

> A lista completa de idiomas e pares suportados pelo LibreTranslate (dependente dos modelos instalados) pode ser consultada em: https://docs.libretranslate.com/guides/supported_languages/

### Tradução

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/translate` | Traduz texto para um ou mais idiomas de destino |

#### `POST /api/translate`

Recebe texto, o idioma de origem e a lista de idiomas de destino, e devolve o registo de tradução.

```json
{
  "htmlContent": "<p>Olá, mundo!</p>",
  "sourceLanguage": "pt",
  "targetLanguages": ["en", "es", "fr"]
}
```

### Histórico

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/publish` | Guarda um registo de tradução no histórico |
| `GET` | `/api/historico` | Lista todos os registos de tradução guardados |
| `GET` | `/api/historico/{fileName}` | Descarrega um registo de tradução (ficheiro JSON) |
| `GET` | `/api/historico/{fileName}/conteudo` | Devolve o conteúdo de um registo de tradução |
| `DELETE` | `/api/historico/{fileName}` | Elimina um registo de tradução |

### Respostas

| Código | Descrição |
|--------|-----------|
| `200` | Pedido processado com sucesso |
| `400` | Pedido inválido (campos em falta ou vazios) |
| `404` | Registo de histórico não encontrado |
| `502` | O LibreTranslate devolveu um erro ou resposta inválida |