# Лабораторные работы 4, 5 и 6. Авиакомпания

Вариант 10. Одна линия проекта: EF Core и ADO.NET, REST API рейсов и бронирований с ролями `Dispatcher` и `Passenger`, клиент Blazor. Ниже сначала сохранено описание API лабораторной №5, затем команды и дополнения работ 4 и 6. Отчёты: `docs/lab4-report.md`, `docs/lab5-report.md`, `docs/lab6-report.md`, `docs/labs-completion-plan.md`.

Это продолжение проекта по варианту 10, но в каталоге лабораторной №4 нет базы данных и предметной области авиакомпании. Что именно там лежит и какое допущение из этого следует, описано в разделе «Связь с лабораторной №4».

## Цель

Показать рабочий ASP.NET Core Web API на Minimal APIs: EF Core, внедрение зависимостей, JWT, ролевую авторизацию, проверку входных данных, `ProblemDetails`, пагинацию, фильтрацию, сортировку, CORS, Swagger и автоматические тесты.

## Связь с лабораторной №4

Перед реализацией просмотрены каталоги:

| Каталог | Что внутри | База и EF Core |
|---|---|---|
| `lab5` | Был пуст | Нет |
| `lab4` | Консольное приложение `LabDownloader` (.NET 8). README самой работы называет её лабораторной №3: локальное скачивание файлов, `async`/`await`, `SemaphoreSlim` | Нет сущностей, миграций и провайдера БД |
| `Lab3` | `ShipmentManagement`: отгрузки стройматериалов, XML и LINQ | Файловое хранилище, не EF Core |
| `lab1` | Учёт овощей | Не авиакомпания |

Поэтому схема авиакомпании создана заново и не подменяет чужие таблицы. Провайдер — SQLite: отдельный сервер и учётные данные для этой машины не требовались, а тесты используют тот же провайдер, что и приложение. Это не база лабораторной №4.

## Стек

- C#, ASP.NET Core, целевая платформа `net8.0`.
- Minimal APIs: `WebApplication.CreateBuilder`, `WebApplication`, `app.Run()`.
- Kestrel. Локальный профиль слушает `http://localhost:5080`.
- EF Core SQLite `8.0.32`, миграции `InitialCreate` и `AddAircraft`.
- JWT Bearer `8.0.32`. Хеш пароля считает `PasswordHasher<User>` из `Microsoft.Extensions.Identity.Core`.
- Swagger / OpenAPI через Swashbuckle `6.9.0`.
- xUnit, Moq и `WebApplicationFactory`.
- Blazor Web App, интерактивность Server, проект `AirlineClient`.

Проверялось SDK `9.0.100` (установлен также SDK `8.0.303`) и общим фреймворком ASP.NET Core `8.0.7`. Пакеты `8.0.32` на этом фреймворке запускаются: это подтвердили `dotnet test` и HTTP-проверка.

## Архитектура и путь запроса

```text
HTTP-эндпоинт
  → сервис (правила, владелец брони, места)
    → репозиторий и IUnitOfWork
      → ApplicationDbContext
        → SQLite
```

Обобщённого репозитория нет: у рейсов, бронирований и пользователей свои интерфейсы. `SaveChanges` и транзакция сосредоточены в `IUnitOfWork`, чтобы сервис не вызывал `DbContext` напрямую. Эндпоинт не содержит правил бронирования и не отдаёт наружу сущности EF.

Роли в коде, Swagger, тестах и этой инструкции: `Dispatcher` и `Passenger`.

Политика `DispatcherOnly` требует аутентифицированного пользователя с ролью `Dispatcher`. Ею закрыты создание, изменение и удаление рейса и общий список бронирований.

### Владение бронированием

Идентификатор пассажира берётся из claim `sub` проверенного JWT.

- Пассажир создаёт бронь только на себя. Поле `passengerId` в теле игнорируется, даже если там чужой идентификатор.
- Диспетчер обязан передать `passengerId` существующего пользователя с ролью `Passenger`. Токен диспетчера сам по себе бронь не создаёт.
- Пассажир читает и отменяет только свою бронь. Чужая существующая бронь даёт `403`. Несуществующая — `404`.
- Диспетчер читает и отменяет любую бронь и видит постраничный список `GET /api/bookings`.
- `GET /api/bookings/my` у обоих ролей возвращает только брони текущего `sub`.

Отмена не удаляет строку: статус становится `Cancelled`, места возвращаются на рейс. Повторная отмена — `409`.

### Места

У рейса есть `TotalSeats` и `AvailableSeats`. Клиент не задаёт число свободных мест. На пассажира и рейс допускается одно активное бронирование, от 1 до 9 мест. Новая бронь запрещена, если статус не `Scheduled`, `Delayed` или `Boarding`, если время вылета уже наступило или свободных мест меньше запрошенных.

Списание мест идёт в транзакции. У рейса есть маркер версии `Version`: EF Core включает его в условие `UPDATE`. Параллельный тест двух запросов на последнее место получил один `201` и один `409`, после чего свободных мест осталось 0. Уникальность номера рейса и email дополнительно держит индекс.

Удалить рейс нельзя, пока на него есть хотя бы одна бронь, включая отменённую: внешний ключ `Restrict`, сервис отвечает `409`. Смена статуса рейса на `Cancelled` сама брони не отменяет.

## Структура

```text
AirlineApi.sln
.config/dotnet-tools.json          локальный dotnet-ef 8.0.32
AirlineApi/
  Program.cs                       конвейер, старт Kestrel
  appsettings.json                 несекретные настройки, пустой JWT-ключ
  Configuration/                   JwtOptions, правила ключа, регистрация DI
  Data/                            DbContext, миграции, начальное наполнение
  Models/                          User, Flight, Booking
  DTOs/                            контракты запросов и ответов
  Repositories/                    запросы EF Core и Unit of Work
  Services/                        регистрация, рейсы, брони, текущий пользователь
  Endpoints/                       MapGroup /api/auth, /api/flights, /api/bookings
  Middleware/                      ProblemDetails без внутренних подробностей
  OpenApi/                         замок Authorize на закрытых операциях
AirlineApi.Tests/                  WebApplicationFactory и xUnit
docs/lab5-audit.md
```

## Конфигурация и секреты

В `appsettings.json` лежат уровень журнала, строка подключения SQLite, издатель и аудитория JWT, срок токена, список CORS и флаги миграций. Поле `Jwt:SigningKey` пустое специально. Пустая строка, ключ короче 32 символов и очевидная заглушка (`changeme`, `placeholder` и похожие) не принимаются: процесс завершается до приёма запросов с `OptionsValidationException`.

Секрет задаётся user-secrets или переменной окружения. Пароль диспетчера туда же. В отслеживаемых JSON, исходниках и примерах ниже настоящих секретов нет.

```powershell
dotnet user-secrets init --project .\AirlineApi\AirlineApi.csproj
dotnet user-secrets set "Jwt:SigningKey" "<случайная-строка-не-короче-32-символов>" --project .\AirlineApi\AirlineApi.csproj
dotnet user-secrets set "Admin:Email" "dispatcher@example.local" --project .\AirlineApi\AirlineApi.csproj
dotnet user-secrets set "Admin:Password" "<пароль-от-8-символов-с-латинской-буквой-и-цифрой>" --project .\AirlineApi\AirlineApi.csproj
```

User-secrets подхватываются в среде `Development`. Профиль запуска выставляет именно её. Для другой среды те же значения можно передать переменными:

| Переменная | Назначение |
|---|---|
| `Jwt__SigningKey` | Подпись JWT. Обязательна |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes` | Несекретные параметры токена, уже заданы в JSON |
| `Admin__Email`, `Admin__Password` | Первичный диспетчер. Оба пустые — учётная запись не создаётся |
| `Admin__FullName` | Имя диспетчера, по умолчанию «Диспетчер» |
| `ConnectionStrings__DefaultConnection` | Замена строки SQLite |
| `Database__ApplyMigrationsOnStartup` | `true` применяет миграции при старте и не удаляет данные |
| `Database__SeedSampleData` | `true` добавляет учебные рейсы, только если таблица рейсов пуста |

Пароль диспетчера хешируется. Если пользователь с этим email уже есть и он диспетчер, пароль при следующем запуске не перезаписывается. Если email занят пассажиром, запуск останавливается и роль сам не повышает. Повторно проверен создающий путь; ветка «email уже занят другой ролью» в HTTP-тесте не выполнялась.

Пароли и ключ не пишутся в журнал. В журнал попадают идентификаторы пользователя, рейса и брони и итог операции.

## База данных

Файл по умолчанию: `AirlineApi/App_Data/airline.db`. Каталог создаётся при старте и исключён в `.gitignore`. Существующий файл не удаляется и не пересоздаётся.

Таблицы миграции `20261017203737_InitialCreate`:

- `Users`: email уникален, хранится хеш пароля и роль.
- `Flights`: уникальный номер, аэропорты, времена, статус, места, маркер версии. Проверка SQLite не даёт отрицательные места и `AvailableSeats > TotalSeats`.
- `Bookings`: внешние ключи на рейс и пассажира с `ON DELETE RESTRICT`.

Применение вручную, из каталога `lab5`:

```powershell
dotnet tool restore
dotnet ef migrations list --project .\AirlineApi\AirlineApi.csproj
dotnet ef database update --project .\AirlineApi\AirlineApi.csproj
```

При `Database:ApplyMigrationsOnStartup=true` тот же шаг выполняется до `app.Run()`. Команда `database update` нужна, если автоматическое применение выключено.

Учебные рейсы, если таблица пуста:

| Номер | Маршрут | Статус | Места | Идентификатор |
|---|---|---|---|---|
| SU100 | SVO → LED | Scheduled | 180 | `11111111-1111-1111-1111-111111111101` |
| SU210 | LED → AER | Delayed | 160 | `11111111-1111-1111-1111-111111111102` |
| DP405 | SVO → KZN | Boarding | 100 | `11111111-1111-1111-1111-111111111103` |
| UT288 | SVX → SVO | Cancelled | 140 | `11111111-1111-1111-1111-111111111104` |
| SU050 | SVO → AER | Departed, 1 сентября 2026 | 120 | `11111111-1111-1111-1111-111111111105` |

Аэропорт хранится кодом IATA из трёх латинских букв. Номер рейса: две латинские буквы и от 1 до 4 цифр. Время без зоны в JSON считается UTC. Прилёт должен быть позже вылета, аэропорты не совпадают.

## Сборка, тесты и запуск

Из каталога `lab5` в PowerShell:

```powershell
dotnet restore .\AirlineApi.sln
dotnet build .\AirlineApi.sln
dotnet test .\AirlineApi.sln
dotnet run --project .\AirlineApi\AirlineApi.csproj --launch-profile http
```

`dotnet run` без ключа не поднимется. Сначала задайте user-secrets или `Jwt__SigningKey`.

Адреса локального профиля:

- API: `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`
- Корень `/` в Development перенаправляет на Swagger.

HTTPS в профиль не включён, чтобы не требовать сертификат разработчика. `UseHttpsRedirection` подключается только если `ASPNETCORE_URLS` содержит `https://`. Для сдачи и локальной проверки используется HTTP. Настоящее развёртывание должно завершать TLS на прокси или у Kestrel; это не замена проверке JWT.

Тесты поднимают приложение через `WebApplicationFactory` на временном файле SQLite. Провайдер тот же. Учебные рейсы в тестах выключены, диспетчер создаётся со случайным паролем на время прогона.

## Как получить JWT

1. Откройте `http://localhost:5080/swagger`.
2. `POST /api/auth/register` создаёт пассажира и сразу возвращает токен. Роль в запросе не выбирается: значение `Dispatcher` даёт `400`, учётная запись не создаётся.
3. `POST /api/auth/login` проверяет email и пароль и возвращает новый JWT.
4. Нажмите **Authorize**, вставьте значение `accessToken` без слова `Bearer` и подтвердите. Swagger сам добавит схему `Bearer`.
5. Для диспетчера войдите email и паролем из user-secrets или `Admin__Email` / `Admin__Password`. Пока оба значения пустые, диспетчера нет: изменение рейсов будет отвечать `403` любому пассажиру и `401` анониму.

Токен живёт `Jwt:AccessTokenMinutes` (по умолчанию 60). Проверяются подпись HMAC-SHA256, издатель `AirlineApi`, аудитория `AirlineApiClients` и срок. Claim `role` содержит `Dispatcher` или `Passenger`, claim `sub` — идентификатор пользователя. `MapInboundClaims` выключен, тип роли в проверке — `role`.

В ответах нет пароля и хеша.

## Эндпоинты

| Метод | Путь | Назначение | Кто |
|---|---|---|---|
| POST | `/api/auth/register` | Регистрация пассажира и выдача JWT | Любой |
| POST | `/api/auth/login` | Проверка пароля и выдача JWT | Любой |
| GET | `/api/flights` | Список с пагинацией, фильтрами и сортировкой | Любой |
| GET | `/api/flights/{id}` | Карточка рейса | Любой |
| POST | `/api/flights` | Создать рейс | `Dispatcher` |
| PUT | `/api/flights/{id}` | Изменить рейс | `Dispatcher` |
| DELETE | `/api/flights/{id}` | Удалить рейс, если броней нет | `Dispatcher` |
| GET | `/api/bookings/my` | Брони текущего пользователя | Вошедший пользователь |
| GET | `/api/bookings/{id}` | Одна бронь: своя или любая для диспетчера | Вошедший пользователь |
| POST | `/api/bookings` | Создать бронь | Вошедший пользователь, по правилам выше |
| DELETE | `/api/bookings/{id}` | Отменить бронь и вернуть места | Владелец или `Dispatcher` |
| GET | `/api/bookings` | Все брони, постранично | `Dispatcher` |

Отсутствующий или недействительный токен на закрытой операции — `401`. Вошедший пользователь без права — `403`.

## Примеры

Регистрация:

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "ivan.petrov@example.com",
  "password": "Passenger_1a",
  "fullName": "Иван Петров"
}
```

Ответ `201`:

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-10-17T21:40:00Z",
  "user": {
    "id": "e91f8d41-da79-47f5-984a-addef277a2dc",
    "email": "ivan.petrov@example.com",
    "fullName": "Иван Петров",
    "role": "Passenger"
  }
}
```

Создание рейса диспетчером:

```http
POST /api/flights
Authorization: Bearer <jwt диспетчера>
Content-Type: application/json

{
  "flightNumber": "ZZ9001",
  "departureAirport": "SVO",
  "arrivalAirport": "LED",
  "departureTime": "2026-12-20T10:00:00Z",
  "arrivalTime": "2026-12-20T12:00:00Z",
  "status": "Scheduled",
  "totalSeats": 3
}
```

Ответ `201` содержит `availableSeats`, равное `totalSeats`.

Бронь пассажира. Чужой `passengerId` не используется:

```http
POST /api/bookings
Authorization: Bearer <jwt пассажира>
Content-Type: application/json

{
  "flightId": "11111111-1111-1111-1111-111111111101",
  "seatCount": 1,
  "passengerId": "00000000-0000-0000-0000-000000000000"
}
```

### Ошибки

Неверный пароль и неизвестный email отвечают одинаково, `401`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Требуется аутентификация",
  "status": 401,
  "detail": "Неверный email или пароль.",
  "instance": "/api/auth/login"
}
```

Попытка выбрать роль диспетчера при регистрации, `400`:

```json
{
  "title": "Некорректный запрос",
  "status": 400,
  "detail": "Регистрация не позволяет выбрать роль Dispatcher. Новые пользователи получают роль Passenger."
}
```

Одинаковые аэропорты и слишком короткий пароль проходят Data Annotations и возвращают `400` с телом `application/problem+json` и словарём `errors`. Пассажир на `POST /api/flights` получает `403`. Неизвестный рейс — `404` с `detail` «Рейс не найден.» Необработанное исключение клиенту отдаёт общий текст `500` без сообщения исключения и без стека. Это проверяет тест на служебном маршруте, который регистрируется только при `Testing:ExposeUnhandledEndpoint=true`. В обычном `appsettings.json` такого флага нет.

### Пагинация, фильтры и сортировка

`GET /api/flights?departureAirport=SVO&arrivalAirport=LED&status=Scheduled&sortBy=departureTime&sortDir=asc&page=1&pageSize=10`

- `page` с 1. Если параметр опущен, страница 1.
- `pageSize` от 1 до 50. Если опущен, размер 10.
- `status`: `Scheduled`, `Delayed`, `Boarding`, `Departed`, `Arrived`, `Cancelled`.
- `sortBy`: только `departureTime`, `arrivalTime`, `flightNumber`. Другое значение — `400`, в SQL оно не подставляется.
- `sortDir`: `asc` или `desc`. По умолчанию вылет по возрастанию, затем номер рейса.
- Ответ: `items`, `totalCount`, `page`, `pageSize`, `totalPages`.

Пример проверенного ответа по вылету из SVO: `totalCount` 3, номера `DP405`, `SU050`, `SU100` при сортировке по номеру. Запрос `sortBy=dropTable` вернул `400` и перечисление разрешённых полей.

`GET /api/bookings` у диспетчера принимает `page`, `pageSize`, `flightId`, `passengerId`, `status`. Порядок фиксирован: дата создания по убыванию, затем идентификатор. Список «мои брони» не страничный: это брони одного пользователя.

## DI, конвейер, CORS и журнал

Время жизни:

- `AddScoped`: `DbContext`, репозитории, `IUnitOfWork`, сервисы приложения и `ICurrentUser`. На запрос один контекст, поэтому транзакция брони видит тот же рейс.
- `AddSingleton`: `PasswordHasher<User>`. У него нет состояния запроса.
- Настройки JWT, администратора и базы читаются через `IOptions<T>`. Обязательные поля JWT проверяются на старте.

Порядок конвейера в `Program.cs`:

1. Обработчик исключений.
2. Перенаправление на HTTPS, только если в адресах есть `https://`.
3. В Development: Swagger, Swagger UI и редирект с `/`.
4. CORS, политика `Frontend`.
5. Аутентификация.
6. Авторизация.
7. Эндпоинты Minimal API.

Миграции выполняются после регистрации конвейера и до `app.Run()`.

CORS читает `Cors:AllowedOrigins`. Политика не использует `AllowAnyOrigin()` и не включает учётные данные cookie. Пустой список не открывает все источники: остаётся только `http://localhost:5080`. CORS ограничивает браузерные запросы с другой страницы и не заменяет JWT. Клиент `AirlineClient` — Blazor Server: страницу открывает браузер, а к API обращается сервер клиента. Для этого вызова CORS не нужен. В конфигурации оставлены типичные адреса Vite, Angular и React-разработки.

Журнал: `ILogger<T>` в сервисах, наполнении базы и обработчике ошибок. Успешные операции — Information, отказ во входе и конфликт версии — Warning или Information без пароля, необработанное исключение — Error уже на сервере. Команда SQL EF Core в конфигурации опущена до Warning, чтобы параметры не попадали в обычный журнал.

## Проверенные ограничения

- Базы лабораторной №4 не существует, поэтому SQLite новый, а не «тот же файл».
- HTTPS-профиль и сертификат не запускались.
- Окно Swagger **Authorize** видно, схема `Bearer` есть в OpenAPI. Сам токен в это окно мышью не вставлялся: те же заголовки проверены HTTP-клиентом.
- Ветка сида «email диспетчера уже занят пассажиром» не прогонялась отдельным тестом.
- Отмена рейса не отменяет его брони автоматически.
- Рейс с любой бронью, даже отменённой, не удаляется.
- Защита мест проверена двумя параллельными запросами в одном процессе к файлу SQLite. Отдельный распределённый замок между несколькими серверами не делался: для одного файла SQLite запись сериализуется самим движком, а конфликт версии закрывает потерянное обновление.

## Фактическая проверка

Команды из каталога `lab5` 17 октября 2026 года:

```text
dotnet build .\AirlineApi.sln
Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

dotnet test .\AirlineApi.sln
Пройден. не пройдено 0, пройдено 26, пропущено 0, всего 26, длительность 3 s.
```

До повышения пакетов до `8.0.32` восстановление `8.0.7` сообщало NU1903 для транзитивных `System.Text.Json` 8.0.4, `Microsoft.Extensions.Caching.Memory` 8.0.0 и `SQLitePCLRaw.lib.e_sqlite3` 2.1.6. Итоговая сборка и тесты этих предупреждений не показали.

Отдельный процесс без `Jwt:SigningKey` завершился до прослушивания порта:

```text
OptionsValidationException: The SigningKey field is required.
Jwt:SigningKey отсутствует или небезопасен...
```

С временными переменными окружения API слушал `http://localhost:5080`. Проверено HTTP-запросами: учебные рейсы, отказ регистрации в диспетчеры (`400`), вход пассажира, `403` пассажира на создание рейса, создание рейса диспетчером (`201`), анонимная бронь (`401`), бронь пассажира с игнорированием чужого `passengerId`, список своих броней, `403` пассажира на общий список, список диспетчера, плохая сортировка (`400`), неизвестный рейс (`404`). `GET /swagger/index.html` и `GET /swagger/v1/swagger.json` вернули `200`; в документе есть все маршруты и схема `Bearer` на закрытых операциях. Страница Swagger в браузере показала группы Auth, Bookings и Flights и кнопку Authorize.

После этой проверки процесс остановлен, а файл `App_Data/airline.db` удалён: в нём оставался хеш временного пароля смоук-теста. Первый локальный запуск с вашими секретами создаст файл заново и, если рейсов ещё нет, запишет учебные рейсы.

## Лабораторные №4 и №6

Сущность `Aircraft`: один самолёт — много рейсов. Пассажир остаётся `User` с ролью `Passenger`. Бронь связывает пассажира и рейс. `aircraftId` у рейса необязателен и без него подставляется резервное судно `RA-0001`.

ADO.NET живёт в `AdoNetDemoService`: параметризованный запрос и транзакция с фиксацией или откатом на той же строке подключения. Чтение для диспетчера: `GET /api/demo/ado/flights-by-status`. Произвольный SQL клиенту не отдаётся. `ExecuteUpdate` и `ExecuteDelete` показаны в тесте, не в проведении брони.

Новые маршруты API: `/api/aircraft`, `/api/passengers` (только диспетчер, без пароля и токена), `/health`.

Клиент: `dotnet run --project AirlineClient --launch-profile http`, адрес `http://localhost:5081`, API `http://localhost:5080`. Токен сеанса хранится через `ProtectedSessionStorage`.

Ключ JWT и пароль диспетчера задаются только окружением:

```text
$env:Jwt__SigningKey = "<случайная строка не короче 32 символов>"
$env:Admin__Email = "dispatcher@local.test"
$env:Admin__Password = "<пароль с буквой и цифрой>"
dotnet run --project AirlineApi --launch-profile http
```

Оба поля администратора пустые — диспетчер не создаётся. Если диспетчер с этим email уже есть, пароль не перезаписывается.

Миграции, фактически выполненные в этом сеансе:

```text
dotnet ef migrations add AddAircraft --project AirlineApi/AirlineApi.csproj --startup-project AirlineApi/AirlineApi.csproj --output-dir Data/Migrations
dotnet ef migrations list --project AirlineApi/AirlineApi.csproj --startup-project AirlineApi/AirlineApi.csproj
dotnet ef database update --project AirlineApi/AirlineApi.csproj --startup-project AirlineApi/AirlineApi.csproj
```

`database update` создал отсутствовавший `AirlineApi/App_Data/airline.db` и применил обе миграции. `migrations remove` не запускался.

Docker:

```text
$env:JWT_SIGNING_KEY = "<случайная строка не короче 32 символов>"
docker compose up --build
```

Снаружи API `http://localhost:5080`, клиент `http://localhost:5081`. Внутри compose клиент вызывает `http://api:8080`. Файл базы контейнера — `/data/airline.db` в томе `airline-data`.

Повторный `dotnet test AirlineApi.sln` после расширения: 40 пройдено, 0 не пройдено. Из них 8 модульных (6 сервис бронирования на Moq, 2 правила формы), 29 интеграционных через `WebApplicationFactory`, 3 интеграционных на временном SQLite для ADO.NET и демонстрации EF Core.

`docker compose build` и `docker compose up -d` выполнены: API стал healthy, оба адреса `/health` ответили успешно, контейнер отдал 5 рейсов. Процессы затем остановлены. Публикация образов в GHCR не выполнялась. Вход, карточка рейса, отказ удаления занятого судна, создание и отмена брони проверены в браузере на `http://localhost:5081`. Локальный `App_Data/airline.db` после этого удалён.
