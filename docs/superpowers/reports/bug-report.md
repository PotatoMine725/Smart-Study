
## Description
When running the application, specifically querying the `HocKy` table with navigation properties `DanhSachMonHoc` and `DanhSachTask`, an `Microsoft.Data.Sqlite.SqliteException` occurred.
The exception details indicated that there was `"no such column: s.NgayHoanThanh"`.

## Root Cause
The root cause was traced back to Entity Framework Core's initial database setup script. The file `App.xaml.cs` utilized `db.Database.EnsureCreated()` logic to construct the `.db` container file natively for the `AppDbContext`.
However, `EnsureCreated` behaves uniquely in that it only generates a database if it doesn't already exist.
A property `NgayHoanThanh` was introduced to the `StudyTask` model after the initial creation, and EF Core's `EnsureCreated` doesn't implement alter scripts for ongoing changes to underlying SQL structures. Ergo, when queries ran referencing models out-of-sync with the file's structure, a runtime exception occurred.

## Applied Fix
The startup initialization method code was altered as follows:
- Implemented `using Microsoft.EntityFrameworkCore;` inside `App.xaml.cs`
- Replaced the call to `db.Database.EnsureCreated()` with `db.Database.Migrate()` 

Now, when executing logic relying on Data Entities, EF Core runs missing alterations dynamically on launch.