# Hotel.Web

Aplicación ASP.NET Core MVC de reservas de hotel con Entity Framework Core (Database First), sin migraciones ni procedimientos almacenados.

- El esquema lo define `db/HotelDB.sql` (TipoHabitacion, Habitacion, Huesped y Reserva).
- El modelo se generó con scaffold en `Models/` con el contexto `HotelContext` y no se edita a mano.
- Todas las consultas LINQ y los `SaveChangesAsync` están en `Repositorios/`; los controladores no usan el contexto.
- Eliminación lógica (`Activo = 0`), patrón Post/Redirect/Get y mensajes con `TempData`.

## Requisitos

- .NET SDK 10 o superior (el proyecto usa `net10.0`)
- SQL Server Express en `.\SQLEXPRESS` con autenticación de Windows
- Visual Studio 2026

## Ejecutar

1. Ejecutar `db/HotelDB.sql` en SQL Server (crea `HotelDB` con datos de prueba).
2. Ejecutar `db/AgregarObservaciones.sql` (agrega `Reserva.Observaciones`).
3. Abrir `Hotel.slnx` y ejecutar con el perfil `http` (http://localhost:5118).

Cadena de conexión (`Hotel.Web/appsettings.json`):

```
Server=.\SQLEXPRESS;Database=HotelDB;Integrated Security=True;TrustServerCertificate=True
```

## Scaffold

Consola del Administrador de paquetes, con `Hotel.Web` como proyecto predeterminado:

```
Scaffold-DbContext "Server=.\SQLEXPRESS;Database=HotelDB;Integrated Security=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models -Context HotelContext -NoOnConfiguring
```

Desde la terminal, dentro de `Hotel.Web` (`dotnet-ef` está en el manifiesto local, `dotnet tool restore`):

```
dotnet ef dbcontext scaffold "Server=.\SQLEXPRESS;Database=HotelDB;Integrated Security=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models --context HotelContext --no-onconfiguring
```

- `-NoOnConfiguring` / `--no-onconfiguring` evita que el contexto traiga `OnConfiguring`; la cadena queda solo en `appsettings.json`.
- Después del `ALTER TABLE` se regeneró con `-Force` / `--force` y apareció `Reserva.Observaciones`.

Los comandos están también en `scripts/comandos.txt`.

## Estructura

```
Lab10/
├── Hotel.slnx
├── db/
│   ├── HotelDB.sql               esquema y datos de prueba
│   └── AgregarObservaciones.sql  ALTER TABLE del paso final
├── scripts/comandos.txt
└── Hotel.Web/
    ├── Models/          generado con scaffold (HotelContext y entidades)
    ├── Repositorios/    HabitacionRepositorio, ReservaRepositorio
    ├── Controllers/     Home, Habitaciones, Reservas
    └── Views/
```

## Reglas implementadas

- Habitaciones: listado de activas con su tipo, filtro por tipo y orden por número; el número no se puede repetir, tampoco con una habitación dada de baja (el `UNIQUE` de la tabla también las incluye). No se da de baja una habitación con reservas pendientes o confirmadas.
- Disponibilidad: una reserva se cruza cuando su estado no es Cancelada, su ingreso es menor que la salida pedida y su salida es mayor que el ingreso pedido. Se resuelve con una sola consulta (`!Any`) en `ReservaRepositorio`.
- Reservas: la salida debe ser posterior al ingreso, el ingreso no puede ser anterior a hoy y la habitación no puede estar reservada en ese rango. Se guardan como Pendiente y solo se cancelan las Pendiente o Confirmada.
- Las validaciones se hacen en los controladores con `ModelState.AddModelError`. Las propiedades de navegación se quitan con `ModelState.Remove` y los `[Bind]` limitan los campos que llegan del formulario.
