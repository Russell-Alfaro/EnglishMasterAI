# Migración de SQLite → PostgreSQL (Neon)

Este proyecto fue migrado de SQLite a PostgreSQL para poder desplegarse en la nube
gratis (Render, Fly.io, etc. no conservan archivos SQLite entre reinicios).

## Qué cambió

- `Infrastructure.csproj`: `Microsoft.EntityFrameworkCore.Sqlite` → `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Program.cs`: `UseSqlite(...)` → `UseNpgsql(...)`
- `appsettings.json`: cadena de conexión ahora es un placeholder de Postgres
- Se eliminaron las migraciones viejas (`Infrastructure/Migrations/`) porque eran
  específicas de SQLite (tipos de columna `TEXT`, etc.) — hay que regenerarlas.
- Se eliminó el archivo `englishmasterai.db` (ya no se usa).

## Pasos para dejarlo funcionando

### 1. Crear tu base de datos gratis en Neon

1. Ve a https://neon.tech y crea una cuenta (no pide tarjeta).
2. Crea un proyecto nuevo, por ejemplo `englishmasterai`.
3. En el dashboard del proyecto, copia el **Connection String** que te da Neon.
   Se ve más o menos así:
   ```
   postgresql://usuario:password@ep-xxxx-xxxx.us-east-2.aws.neon.tech/englishmasterai?sslmode=require
   ```

### 2. Convertir esa URL al formato que usa Npgsql

Npgsql no usa el formato `postgresql://...`, usa formato `Host=...;Database=...`.
Tomando el ejemplo de arriba, quedaría así:

```
Host=ep-xxxx-xxxx.us-east-2.aws.neon.tech;Database=englishmasterai;Username=usuario;Password=password;SSL Mode=Require;Trust Server Certificate=true
```

### 3. Configurar la cadena de conexión (NUNCA la subas a Git en texto plano)

**Para desarrollo local**, la forma más fácil y segura es usar los "user secrets" de .NET
en vez de escribirla directo en `appsettings.json`:

```powershell
cd src\EnglishMasterAI.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=TU-HOST.neon.tech;Database=englishmasterai;Username=TU-USUARIO;Password=TU-PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

Esto guarda la cadena fuera del proyecto (en tu perfil de Windows), así que no corres
riesgo de subirla sin querer a un repositorio público.

*(Si prefieres algo más simple para probar rápido, puedes pegarla directo en
`appsettings.json` reemplazando el placeholder — solo asegúrate de NO subir ese
archivo con la contraseña real a GitHub si el repo es público).*

### 4. Restaurar paquetes y regenerar las migraciones

Desde la raíz del proyecto (donde está el `.slnx`):

```powershell
dotnet restore

cd src\EnglishMasterAI.Infrastructure
dotnet ef migrations add InitialPostgres --startup-project ..\EnglishMasterAI.API
```

Si te dice que falta la herramienta `dotnet-ef`, instálala una vez con:
```powershell
dotnet tool install --global dotnet-ef
```

### 5. Aplicar la migración (crear las tablas en Neon)

No hace falta ejecutar `dotnet ef database update` manualmente — el `Program.cs`
ya llama a `db.Database.Migrate()` automáticamente al iniciar la API. Solo corre:

```powershell
cd ..\EnglishMasterAI.API
dotnet run
```

En la consola deberías ver que aplica la migración `InitialPostgres` y crea las
tablas `Students`, `Lessons`, `Practices` directamente en tu base de Neon.

### 6. Verificar

Abre `http://localhost:5050` (Swagger) y prueba registrar un estudiante con
**POST /api/students**. Si todo salió bien, puedes verlo en el dashboard de Neon
(pestaña "Tables" o "SQL Editor" → `SELECT * FROM "Students";`).

---

## Nota sobre el bug de concurrencia anterior

El error `DbUpdateConcurrencyException` que tenías con SQLite era un bug del
proveedor `Microsoft.Data.Sqlite` al reportar mal las filas afectadas cuando un
mismo `SaveChanges` incluía un INSERT y un UPDATE juntos. PostgreSQL/Npgsql no
tiene ese problema, pero de todas formas dejamos el código dividido en dos pasos
(`RegisterLesson` + `ApplyLessonProgress`, `RegisterPractice` + `ApplyPracticeProgress`)
porque es una forma más clara y segura de escribir la lógica, sin ningún costo real.
