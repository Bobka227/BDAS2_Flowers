# BDAS2_Flowers – internetový obchod s květinami

BDAS2_Flowers je školní projekt internetového obchodu s květinami postavený na **ASP.NET Core MVC (.NET 8)** a databázi **Oracle**.  
Aplikace řeší kompletní proces od registrace zákazníka, nákupu z katalogu produktů, vytváření objednávek a recenzí až po administrativní správu dat (produkty, objednávky, platby, zaměstnance, události atd.).

---

## Technologický stack

- **Backend**
  - .NET 8.0, ASP.NET Core MVC
  - Oracle.ManagedDataAccess.Core – oficiální .NET driver pro Oracle
  - Vlastní datová vrstva (`IDbFactory`, `OracleDbFactory`, `DbRead`)
  - Vlastní autentizace / autorizace přes cookie, role CUSTOMER/ADMIN
  - Hashování hesel – `BDAS2_Flowers.Security.HmacSha256PasswordHasher`

- **Frontend**
  - Razor Views (.cshtml), HTML5, CSS3, JavaScript
  - Bootstrap 5 + Bootstrap Icons
  - Vlastní styly v `wwwroot/css/site.css` a skripty v `wwwroot/js/site.js`

- **Vývojové nástroje**
  - Microsoft Visual Studio 2022 Community
  - Oracle SQL Developer (správa databáze, PL/SQL)
  - Cisco AnyConnect (VPN do univerzitní sítě)
  - Git (integrovaný ve Visual Studiu)

---

## Hlavní funkce aplikace

### Uživatelská část (CUSTOMER)

- **Registrace a přihlášení**
  - Registrace nového účtu (e-mail, heslo, jméno, příjmení, telefon).
  - Přihlášení formulářem, hesla uložená pouze v hashované podobě.
  - Po přihlášení cookie s claimy: UserId, Email, Name, Role.

- **Katalog a košík**
  - Prohlížení katalogu produktů (kategorie, vyhledávání).
  - Přidávání produktů do košíku (uložen v session).
  - Zobrazení náhodně vybraných „featured“ produktů na úvodní stránce.

- **Objednávky**
  - Vytvoření běžné objednávky (doručení zboží):
    - výběr doručení, prodejny a adresy (existující / nová),
    - volba platby: karta, hotově, kupón,
    - validace částky hotově a kupónu (platnost, hodnota).
  - Vytvoření objednávky **akce** (organizace výzdoby):
    - zadání místa a data akce,
    - povinný balíček „Organizace akce (poplatek)“,
    - volitelné balíčky doplňkových služeb (fotograf, dort, výzdoba…),
    - předběžný součet ceny doplňkových služeb.

- **Profil uživatele**
  - Přehled základních údajů (jméno, e-mail, role, segment zákazníka).
  - Historie objednávek a používané adresy.
  - Změna e-mailu a hesla (s ověřením stávajícího hesla).
  - Nahrání vlastního avataru (uložen jako BLOB v Oracle).

- **Recenze**
  - Přihlášený uživatel může přidat recenzi (hvězdičky + text, volitelně město).
  - Všechny recenze se zobrazují na veřejné stránce „Recenze“.

### Administrace (ADMIN)

Role **ADMIN** má přístup do administrativního panelu (`/admin`) a může:

- spravovat **uživatele** (role, mazání, impersonace zákazníka),
- spravovat **produkty, typy produktů, objednávky, platby, prodejny, adresy, kupóny, recenze, zaměstnance, pracovní pozice, typy a záznamy událostí, obrázky**,
- prohlížet a čistit **systémové logy** (DELETE/TRUNCATE, reset sekvencí),
- nahlížet na **DB objekty** přes speciální admin přehled.

---

## Architektura

- **Controllers** – logika pro jednotlivé domény:
  - `AuthController`, `ProfileController`, `OrdersController`, `OrderEventController`, `CatalogController`, `ReviewsController`, `MediaController`, `CartController`, …  
  - Admin část: `AdminHomeController`, `AdminProductsController`, `AdminOrdersController`, `AdminPaymentsController`, `AdminUsersController`, atd.
- **Models.ViewModels** – view modely pro každou stránku (např. `OrderCreateVm`, `OrderDetailsVm`, `ProfileVm`, `ReviewsPageVm`, `Admin*Vm`).
- **Data** – přístup k databázi:
  - `IDbFactory`, `OracleDbFactory` – jednotné otevírání spojení, nastavení `ClientIdentifier` pro audit v PL/SQL.
  - `DbRead` – pomocné metody pro bezpečné čtení hodnot z `OracleDataReader`.
- **Security** – hashování hesel a konfigurace cookie autentizace.

Veškeré zápisy do databáze probíhají přes **PL/SQL procedury a funkce**, databáze nabízí také řadu **view** (např. `VW_CATALOG_PRODUCTS`, `VW_ORDER_DETAILS`, `VW_ADMIN_PAYMENTS`, `VW_LOGS_ADMIN`), které aplikace používá pro čtení dat.

---

## Spuštění projektu

1. Naklonuj repozitář a otevři solution ve **Visual Studio 2022**.
2. Do `appsettings.Development.json` (nebo User Secrets) doplň:
   - připojovací řetězec k Oracle (`ConnectionStrings:Oracle`),
   - hodnotu `Auth:Pepper` pro hashování hesel.
3. Ujisti se, že databáze obsahuje:
   - všechny tabulky, sekvence, view a PL/SQL objekty (procedury, funkce, balíky),
   - alespoň jednoho administrátora v tabulce `USER` (ROLE = ADMIN).
4. Spusť aplikaci (F5). Výchozí stránka je `/` (Home/Index).

---

## Řízení přístupu

- **Veřejné stránky**: hlavní stránka, katalog, kontakty, registrace, přihlášení.
- **[Authorize]**: přístup pouze pro přihlášené uživatele (např. profil, objednávky, recenze).
- **[Authorize(Roles = "Admin")]**: přístup pouze pro administrátory (admin panel a všechny admin kontroléry).

---

## Další poznámky

- Logování změn v databázi je řešeno přes vlastní logovací tabulku a view `VW_LOGS_ADMIN`.  
  V admin panelu lze logy stránkovat, čistit a nově také **exportovat do CSV**.
- Segmentace zákazníků (`NEW`, `BRONZE`, `SILVER`, `GOLD`, `INACTIVE`) je počítána v Oracle funkcí `FN_USER_SEGMENT` a zobrazuje se jak v admin přehledu uživatelů, tak v uživatelském profilu.

