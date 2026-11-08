# StellarDrive AutoHost 0.1.2-alpha.1 — wersja do testów

Mod uruchamia natywny serwer Steam/FishySteamworks bez lokalnego gracza. Nie jest oficjalnym serwerem dedykowanym gry. Nie zawiera gry ani cudzych modów. Wymaga własnej instalacji StellarDrive Demo 0.8.2, MelonLoader 0.7.3 w wariancie net35, Windows x64 i zalogowanego klienta Steam w tej samej sesji użytkownika.

**Nie potwierdzono jeszcze działania na Waszym VPS-ie, dołączenia drugiego konta Steam ani ciągłej pracy 24/7.** Status `ready` oznacza lokalny start transportu i załadowanie świata, nie pomyślny test zdalnego gracza. Zobacz raport i evidence.json.

## Instalacja w osobnej kopii

1. Zatrzymaj tę kopię gry. Zrób kopię całego katalogu świata, wraz z plikami dodatków.
2. Skopiuj `Mods/StellarDrive.AutoHost.dll` do `Mods` obok `StellarDrive.exe`. MelonLoader musi już działać; nie instaluj BepInEx. Nie używaj jednocześnie argumentu `-dedicated` z wcześniejszego prototypu serwera.
3. Zachowaj wszystkie używane mody i ich zależności. Klienci potrzebują takich samych wersji modów części. Samego AutoHost nie potrzebują do natywnego dołączania Steam (wymaga testu drugiego konta).
4. Skopiuj skrypty i `autohost.json` do np. `S:\AutoHost`. W `WorldPath` wpisz pełną ścieżkę do istniejącego `world.json`, np. `S:\saves\DedicatedWorld\world.json`. Każdy świat musi mieć własny katalog. Nie wkładaj kilku światów pod jedną nazwę.
5. Steam na VPS ma być uruchomiony i zalogowany do konta z dostępem do Demo. Mod pozostawia natywne sprawdzenie uprawnień Steam. Konto nie powinno być równocześnie wykorzystywane do gry na komputerze gracza; takiej konfiguracji nie przetestowano.
6. Uruchom PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "S:\AutoHost\Start-AutoHost.ps1" -GameDir "S:\steam\steamapps\common\StellarDrive Demo" -Config "S:\AutoHost\autohost.json"
```

Skrypt uruchamia proces z `-batchmode -nographics` i ukrywa konsolę oraz ekran startowy MelonLoadera. Dla bezpośredniego uruchomienia EXE tworzy brakujący `steam_appid.txt` z identyfikatorem Demo `4689730`; nie nadpisuje innego identyfikatora i nie omija sprawdzeń Steam. Nie zmienia zapory, systemu, grafiki ani konta Steam. Domyślnie dopuszcza 3 ponowienia po awarii w ciągu 15 minut, z opóźnieniami 10/20/40 s. Kod 0 kończy nadzór, kod 2 zatrzymuje go jako błąd konfiguracji/startu. Nie zabija zawieszonego procesu automatycznie.

## Konfiguracja

- `AutoHost`: włączanie automatycznego startu.
- `WorldPath`: dokładny zapis. Mod nie zgaduje „ostatniego świata”; ostatnia wpisana ścieżka jest trwałym wyborem.
- `MaxPlayers`: 1–32766; deklarowana pojemność nie jest gwarancją wydajności.
- `FriendsOnly`: kontrola znajomości Steam dla identyfikatora połączenia transportowego. To nie widoczność lobby. Test rzeczywistego znajomego i obcego konta jest jeszcze wymagany.
- `AutoSaveMinutes`: od 1/3 minuty (20 s) do 60 minut. AutoHost zastępuje natywny licznik autozapisu; suwak GameMode nie steruje tym interwałem.
- `PauseWhenEmpty`: zatrzymanie etapów fizyki/aktualizacji świata, gdy nie ma graczy. Dodatki mogą mieć osobne timery; ich zgodność trzeba ocenić na zapisach.
- `HeadlessShaderCompatibility`: eksperymentalna poprawka wyboru materiałów w fabrykach części Odyssey Building/Power. Nie zmienia DLL tych dodatków. Pozwala ominąć test wizualnej właściwości `_Albedo` przy urządzeniu graficznym `Null`, zachowując tworzenie części. Nie jest deklaracją pełnej zgodności wszystkich dodatków. W dołączonej konfiguracji dla paczki 39 modów ustawiono `true`, tak jak w testach. Przy pominięciu pola w konfiguracji kod przyjmuje `false`.
- `StartupTimeoutSeconds`: limit 30–900 s. Duże paczki długo tworzą części; dla 39 modów użyj 600 s.

## Dołączanie Steam

Gra używa `SteamFriends.SetRichPresence("connect", "+connect <SteamID>")` i natywnego FishySteamworks. Kod gry nie tworzy lobby SteamMatchmaking. Dlatego nie ma fikcyjnego `lobbyVisibility` ani osobnego przycisku lobby.

Znajomy powinien użyć **Dołącz do gry** przy koncie VPS w Steam. Wymagany jest rzeczywisty test na drugim koncie: widoczność przycisku, połączenie, ruch i budowanie, rozłączenie oraz ponowne wejście. Pozytywny wynik SetRichPresence nie dowodzi działania tej całej ścieżki.

To transport Steam P2P/relay, nie serwer IP na UDP 27863. Skrypt nie ustawia takiego portu. Nie zakładaj, że otwarcie jednego UDP wystarczy przy restrykcyjnej sieci VPS; sprawdź logi Steam i wymagania sieciowe dostawcy. Nie wyłączaj zapory w całości.

## Zapis i wyłączanie

`status.json` pokazuje fazę, PID, rodzaj grafiki, liczbę graczy i liczbę potwierdzonych zapisów. `content-inventory.json` zawiera załadowane mody i części. `unity-errors.log`, log gracza i `MelonLoader/Latest.log` służą do diagnostyki. Pliki mogą zawierać nazwy kont i ścieżki; sprawdź je przed publikowaniem.

Przed zapisem mod tworzy `world.json.autohost-lastgood`, następnie korzysta z natywnego GameSaver i jego kopii zapasowych. Pomija wyłącznie miniaturę wymagającą GPU. Potwierdza zmianę pliku i poprawność JSON. Blokada `world.json.autohost.lock` zapobiega równoczesnemu otwarciu przez drugi AutoHost; zwykła gra nie respektuje tej blokady, więc nie otwieraj tego samego zapisu innym procesem.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "S:\AutoHost\Stop-AutoHost.ps1" -DataDir "S:\AutoHost"
```

Poczekaj na `phase: stopped` oraz zakończenie procesu. Błąd zapisu nie jest zgodą na wymuszone zabicie. Przed kolejnym startem przenieś `supervisor.stop` do archiwum. Do ręcznego zapisu dodaj linię `save` do `commands.txt`; polecenia po przetworzeniu są archiwizowane.

Natywny zapis nie jest atomową transakcją wszystkich modów. Awaria zasilania podczas zapisu i odtwarzanie po niej wymagają osobnego testu. Kopia `lastgood` nie zastępuje kopii całego katalogu świata.

## Start po restarcie Windows

Dołączony `Register-LogonTask.ps1` rejestruje zadanie **po logowaniu użytkownika**, z opóźnieniem 60 s, bez uprawnień administratora. Uruchamiasz go sam na VPS:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "S:\AutoHost\Register-LogonTask.ps1" -GameDir "S:\steam\steamapps\common\StellarDrive Demo" -Config "S:\AutoHost\autohost.json"
```

Steam musi wystartować i zalogować się w tej sesji. Nie skonfigurowano automatycznego logowania Windows ani usługi przed logowaniem. **Sam restart VPS bez dostępnej sesji Steam nie gwarantuje wznowienia hosta.** Rozłączenie RDP i wylogowanie to różne operacje; wpływ polityki kończenia sesji należy sprawdzić na VPS. Nie zapisuj haseł w skryptach.

## Ograniczenia wersji

- Windows Server 2022/VPS i drugi komputer Steam: niezweryfikowane.
- Pełny zestaw 39 modów wymaga dalszych testów rozgrywki. Błędy shaderów/rendererów są rejestrowane, a nie ukrywane.
- Brak lokalnego gracza oznacza brak zwykłego gracza z flagą hosta: funkcje GameMode wymagające tej flagi nie otrzymują automatycznie administratora. Nie nadano wszystkim graczom uprawnień hosta.
- Sprzęt StageSession jest osobnym niedokończonym projektem i nie jest częścią tej paczki.
- Nie ma monitorowania sprzętowego zawieszeń ani gwarancji 24/7. Supervisor obsługuje zakończenie procesu, nie wszystkie rodzaje zacięć.

## Źródła i budowanie

`Source` zawiera własny kod. Wymagany SDK .NET 8 oraz lokalne pliki zgodnej gry i loadera:

```powershell
dotnet build Source\AutoHost.csproj -c Release -p:GameDir="S:\steam\steamapps\common\StellarDrive Demo"
```

Cel .NET Framework 4.7.2 działa przez Mono loadera net35; nie zmieniaj loadera na net6 na podstawie nazwy celu kompilacji. Żadne pliki gry nie są dołączone.

Dokumentacja: [Steam Rich Presence](https://partner.steamgames.com/doc/api/ISteamFriends#SetRichPresence), [inicjalizacja Steam](https://partner.steamgames.com/doc/api/steam_api#SteamAPI_Init), [Unity headless](https://docs.unity3d.com/6000.0/Documentation/Manual/desktop-headless-mode.html).
