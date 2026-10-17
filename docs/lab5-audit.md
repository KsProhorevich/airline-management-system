# Аудит лабораторной работы №5

Вариант 10: REST API рейсов и бронирований, роли `Dispatcher` и `Passenger`.

Дата проверки: 17 октября 2026. Каталог: `lab5`. Команды запускались из этого каталога.

Статусы: `PASS` — сделано и проверено; `PARTIAL` — сделано не полностью или проверено частично; `MISSING` — нет; `UNVERIFIED` — есть в коде, но этот сценарий не исполнялся.

## Осмотр исходного репозитория

| Требование | Статус | Где видно | Проверка |
|---|---|---|---|
| Осмотреть `lab5` до правок | PASS | Каталог был пуст | `Get-ChildItem` по `lab5` не нашёл файлов |
| Найти реализацию и базу лабораторной №4 | PARTIAL | Соседний `lab4/LabDownloader` | README `lab4` описывает лабораторную №3, консольный загрузчик файлов. EF Core, миграций, таблиц и авиационных сущностей нет. `Lab3` — XML-отгрузки, `lab1` — овощи. Новая схема SQLite не выдаётся за базу №4 |
| Зафиксировать SDK, TFM и тесты | PASS | `AirlineApi.csproj`, `README.md` | SDK `9.0.100` и `8.0.303`, общий фреймворк ASP.NET Core `8.0.7`, целевая платформа `net8.0` как у соседних работ |

## Технологии и слои

| Требование | Статус | Где видно | Проверка |
|---|---|---|---|
| Minimal APIs, `CreateBuilder`, `Run`, Kestrel | PASS | `AirlineApi/Program.cs`, `Properties/launchSettings.json` | Процесс слушал `http://localhost:5080`, в журнале `Now listening` |
| EF Core и провайдер | PASS | `Data/ApplicationDbContext.cs`, пакет Sqlite `8.0.32` | Миграция применяется в тестах и при смоук-запуске. Провайдер выбран заново: в №4 его не было |
| Внедрение зависимостей и конструкторы | PASS | `Configuration/ServiceRegistration.cs`, сервисы и репозитории | Scoped для контекста, репозиториев, unit of work и сервисов; singleton только у `PasswordHasher` |
| Обобщённый репозиторий не добавлен без нужды | PASS | `Repositories/IFlightRepository.cs`, `IBookingRepository.cs`, `IUserRepository.cs`, `IUnitOfWork.cs` | Три предметных репозитория и общая точка `SaveChanges`/транзакции. Решение записано в README |
| DTO вместо сущностей EF | PASS | `DTOs/Auth`, `DTOs/Flights`, `DTOs/Bookings` | Ответы не содержат `passwordHash` и навигационных полей EF. Тест регистрации разбирает JSON |
| `IOptions<JwtOptions>` и проверка на старте | PASS | `Configuration/JwtOptions.cs`, `JwtKeyRules.cs`, `ServiceRegistration.cs` | Тесты `Host_does_not_start_with_unsafe_signing_key` и отдельный `dotnet run` без ключа: `OptionsValidationException` до открытия порта |
| `ILogger<T>` | PASS | `AuthService`, `FlightService`, `BookingService`, `DatabaseInitializer`, `ApiExceptionHandler` | Тест регистрации: в собранных сообщениях нет пароля и тестового ключа. Смоук-журнал пишет id диспетчера, не пароль |
| Data Annotations и реальная проверка Minimal API | PASS | DTO, `Endpoints/ValidationFilter.cs`, правила в сервисах | Тесты короткого пароля, одинаковых аэропортов, плохого номера и времён дают `400` и `application/problem+json` |
| `ProblemDetails` | PASS | `Middleware/ApiExceptionHandler.cs`, `ExceptionProblemMapper.cs`, события JWT | Смоук: `400`, `401`, `403`, `404` в формате problem+json. Тест `500` не содержит текст `boom-secret-detail` и имя исключения |
| Swagger и ввод JWT | PARTIAL | `ServiceRegistration.cs`, `OpenApi/AuthorizeOperationFilter.cs` | `GET /swagger/index.html` и `/swagger/v1/swagger.json` дали `200`. В документе схема `Bearer`, у `POST /api/flights` есть security, у списка рейсов её нет. Кнопка Authorize видна в браузере. Токен в это окно мышью не вставлялся; заголовок проверен HTTP-клиентом |
| CORS без `AllowAnyOrigin` вместе с учётными данными | PASS | `ServiceRegistration.AddAirlineServices`, `appsettings.json` | Политика `Frontend`, явные origin. Тест: `http://localhost:5173` получает заголовок, `http://evil.example` — нет. `AllowCredentials` не включён |
| Автотесты важного поведения | PASS | `AirlineApi.Tests` | `dotnet test AirlineApi.sln`: 26 пройдено, 0 не пройдено, около 3 с. Провайдер тестов — тот же SQLite, отдельный временный файл |

## Предметная область

| Требование | Статус | Где видно | Проверка |
|---|---|---|---|
| Рейс: номер, аэропорты, времена, статус, места | PASS | `Models/Flight.cs`, `DTOs/Flights/FlightDtos.cs` | Создание, чтение, список и обновление в `FlightEndpointTests` и HTTP-смоуке |
| Бронь: рейс, пассажир, дата, статус, число мест | PASS | `Models/Booking.cs`, `DTOs/Bookings/BookingDtos.cs` | Ответ содержит id рейса и номер рейса из связи, а не копию паспорта пассажира |
| Не доверять `passengerId` от пассажира | PASS | `Services/BookingService.ResolvePassengerId` | Тест и смоук: в теле чужой id, в ответе id из токена |
| Диспетчер создаёт бронь по правилам | PASS | `BookingService`, README | Без `passengerId` — `400`. С id пассажира — `201`, владелец указанный пассажир |
| Запрет овербукинга и конкурентное обновление | PASS | `BookingService.CreateAsync`, `Flight.Version`, проверка `CK_Flights_Seats` | Последовательный тест занимает все места и следующий получает `409`. Параллельный тест: один `201`, один `409`, свободно 0 мест |
| Удаление рейса при существующих бронях | PASS | `FlightService.DeleteAsync`, `DeleteBehavior.Restrict` | Тест диспетчера: удаление рейса с бронью — `409` |
| Пользователь для аутентификации | PASS | `Models/User.cs`, миграция `Users` | В №4 такой сущности не было, добавлена минимальная и покрыта миграцией |

## Эндпоинты и матрица доступа

| Требование | Статус | Где видно | Проверка |
|---|---|---|---|
| `GET /api/flights` и `GET /api/flights/{id}` без входа | PASS | `Endpoints/FlightEndpoints.cs` | Анонимные тесты и смоук |
| Создание, изменение, удаление рейса только диспетчером | PASS | Политика `DispatcherOnly` | Аноним `401`, пассажир `403`, диспетчер `201`/`200`/`204` |
| `POST /api/auth/register` и `login` | PASS | `Endpoints/AuthEndpoints.cs`, `AuthService` | `201` и `200`, дубликат email `409`, неверный пароль `401` |
| Регистрация не выдаёт `Dispatcher` | PASS | `AuthService.RegisterAsync` | Тест и смоук: `400`, токена в ответе нет |
| `GET /api/bookings/my` только свои | PASS | `BookingService.ListMineAsync` | Два пассажира: чужая бронь не входит в список |
| `GET/DELETE /api/bookings/{id}` | PASS | `BookingService.GetAsync`, `CancelAsync` | Своя `200`/`204`, чужая `403`, отсутствие `404`, диспетчер читает и отменяет |
| `POST /api/bookings` | PASS | `BookingEndpoints` | Аноним `401`, ссылка на рейс проверяется, отменённый и вылетевший рейс — `409` |
| `GET /api/bookings` для диспетчера | PASS | `BookingEndpoints.List` | Пассажир `403`, диспетчер получает страницу. Маршрут добавлен сверх минимального списка, чтобы диспетчер мог вести брони |
| Пагинация, фильтры, сортировка, предел страницы | PASS | `FlightService.ListAsync`, `PaginationRules` | Тест: фильтр аэропорта и статуса, две страницы, порядок по номеру и по вылету, `page=0`, `pageSize=1000`, неизвестные `sortBy` и статус — `400` |
| Сортировка не склеивается в SQL | PASS | `FlightRepository.SearchAsync` | После проверки остаётся только `switch` по перечислению `FlightSortField` |

## Безопасность

| Требование | Статус | Где видно | Проверка |
|---|---|---|---|
| Подпись, издатель, аудитория, срок | PASS | `Configuration/JwtBearerConfigurator.cs`, `AuthService.IssueToken` | Тест читает `iss`, `aud`, `sub`, `role` и срок больше 50 минут. Защищённые вызовы с этим токеном проходят, поддельный токен — `401` |
| Секрет не в репозитории | PASS | `appsettings.json` (`SigningKey` пустой), `.gitignore` | Поиск по исходникам не находит рабочего ключа. Смоук-файл базы после проверки удалён |
| Хеш пароля, не открытый текст | PASS | `AuthService`, `DatabaseInitializer` | `PasswordHasher<User>`. В JSON ответа нет полей пароля |
| Роли `Dispatcher` и `Passenger` согласованы | PASS | `Models/Roles.cs`, claim `role`, политика | Пассажир не создаёт рейс, диспетчер создаёт. `MapInboundClaims = false` |
| `401` и `403` | PASS | События JWT и `ForbiddenException` | Матрица выше. Тело — problem+json |
| Хотя бы одна политика | PASS | `AuthorizationPolicies.DispatcherOnly` | Назначена на изменение рейсов и общий список броней |
| Нет стека, пароля и ключа в ответе клиенту | PASS | `ExceptionProblemMapper` | Модульный тест маппера и HTTP `500` в среде Testing. Журнал регистрации не содержит пароль |

## Конвейер, конфигурация, EF

| Требование | Статус | Где видно | Проверка |
|---|---|---|---|
| Порядок: ошибки, CORS, аутентификация раньше авторизации, эндпоинты | PASS | `Program.cs` | Код конвейера. Запросы смоука проходят аутентификацию до проверки роли: без токена `401`, с токеном пассажира `403` |
| HTTPS не мешает локальному запуску | PARTIAL | `Program.cs`, `launchSettings.json` | Профиль только HTTP, приложение поднялось. Ветка `UseHttpsRedirection` включается при `https://` в `ASPNETCORE_URLS` и отдельно с сертификатом не запускалась |
| Миграции без удаления данных | PASS | `Data/Migrations/20261017203737_InitialCreate.cs`, `DatabaseInitializer` | `MigrateAsync` под общим замком процесса. `EnsureDeleted`/`EnsureCreated` нет. Повторный старт на уже созданной базе в тесте не требуется: каждый прогон получает новый файл, а смоук один раз создал схему и данные |
| Асинхронные запросы, `AsNoTracking` на чтении, страницы без загрузки всей таблицы | PASS | Репозитории | `Count` + `Skip`/`Take`. Карточки броней подгружают рейс и пассажира одним `Include`, не циклом |
| N+1 в списках | PASS | `FlightRepository.SearchAsync`, `BookingRepository.SearchAsync` | Список рейсов не тянет коллекцию броней. Список броней включает ссылки многие-к-одному |
| Начальные данные явные | PASS | `DatabaseSeeder` внутри `DatabaseInitializer` | Смоук: «Sample flights seeded» и «Dispatcher account created». Файл затем удалён |
| Уже существующий диспетчер не получает новый пароль и чужая роль не повышается | UNVERIFIED | `DatabaseInitializer.EnsureDispatcherAsync` | Создание с пустой таблицы проверено. Повторный запуск с тем же email и отказ повысить пассажира до диспетчера кодом предусмотрены, отдельным тестом не исполнялись |

## Команды и результат

```text
dotnet build .\AirlineApi.sln
Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

dotnet test .\AirlineApi.sln
Пройден. не пройдено 0, пройдено 26, пропущено 0, всего 26, длительность 3 s.
```

Смоук с временными `Jwt__SigningKey`, `Admin__Email` и `Admin__Password` на `http://localhost:5080`:

- учебные рейсы `DP405`, `SU050`, `SU100` в фильтре `departureAirport=SVO`;
- регистрация роли Dispatcher — `400`;
- регистрация пассажира — `201`, роль `Passenger`;
- неверный пароль — `401`;
- пассажир создаёт рейс — `403`, диспетчер — `201`;
- анонимная бронь — `401`;
- бронь пассажира игнорирует чужой `passengerId`;
- общий список броней пассажира — `403`, диспетчера — `200`;
- `sortBy=dropTable` — `400`;
- неизвестный GUID рейса — `404`;
- Swagger UI открыт в браузере, заголовок Airline API, группы Auth, Bookings, Flights, кнопка Authorize.

Запуск без ключа:

```text
OptionsValidationException: DataAnnotation validation failed for 'JwtOptions' members: 'SigningKey'
Jwt:SigningKey отсутствует или небезопасен...
```

## Что не считается выполненным полностью

- Повторное использование базы лабораторной №4: базы не было. Это не `PASS`.
- Перенаправление HTTPS на живом сертификате не проверялось.
- Ввод токена именно в диалог Swagger Authorize не выполнялся.
- Повторный сид диспетчера и отказ повысить уже существующего пассажира не исполнялись.
