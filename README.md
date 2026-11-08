# StellarDrive Dedicated AutoHost — 0.3.0-alpha.1

Instalator Windows i panel dla rzeczywistego prototypu AutoHost 0.1.2-alpha.1. **To wydanie eksperymentalne, nie potwierdzony serwer produkcyjny 24/7.** Lokalnie sprawdzono natywny serwer Steam/FishNet bez lokalnego gracza i bez grafiki. Nie wykonano testu drugiego konta Steam ani Windows VPS. Nie nazywamy samego procesu działającym multiplayerem.

## Instalacja bez kompilowania

1. Na VPS przygotuj własną instalację StellarDrive Demo 0.8.2 i klienta Steam. Zaloguj Steam w tej samej sesji Windows, w której będzie działał serwer.
2. Skopiuj **cały folder świata**, razem z plikami dodatków, do osobnego katalogu na dysku z miejscem, np. `S:\saves\DedicatedWorld`. Zrób kopię zapasową. Użyj świata zgodnego z zainstalowaną paczką części.
3. Uruchom `StellarDrive-Server-Setup.exe`. Wybierz folder gry (wykrywany z bibliotek Steam) oraz `world.json`. Kliknij **Dalej → Instaluj → Zakończ**.
4. Instalator może pobrać brakujący MelonLoader 0.7.3 z oficjalnego wydania GitHub. Wymaga wtedy internetu; sprawdza przypięty SHA-256. Innego loadera ani BepInEx nie zastępuje. Instaluje także wszystkie 39 modów Community 1.2.0 z oryginalnych źródeł, sprawdzając archiwa i każdy plik SHA-256. Nie usuwa innych modów.
5. Panel otwiera się po instalacji. Sprawdź ustawienia i wybierz **START**. Statusy Steam i multiplayer pochodzą z pliku zapisanego przez moda, nie z samej obecności procesu.
6. Po starcie panel można zamknąć. Proces PowerShell nadzoruje grę niezależnie od panelu. **Ta niezależność wynika z implementacji; końcowy test GUI na VPS pozostaje wymagany.**
7. **STOP I ZAPIS** prosi mod o zapis i zamknięcie, czeka na proces oraz nadzorcę. Nie wymusza zabicia gry. Zatrzymaj serwer w ten sposób przed restartem Windows.

Pliki programu trafiają do `<gra>\StellarDriveServer`, a mod do `<gra>\Mods\StellarDrive.AutoHost.dll`. Nie zmieniamy `Assembly-CSharp.dll`. Instalator nie wymaga ręcznej instalacji .NET. EXE są self-contained x64 i nie są podpisane certyfikatem wydawcy.

## Wymagania i ograniczenia

- Windows x64 z interaktywną sesją użytkownika, Windows PowerShell 5.1 i Steam. Windows 10 był środowiskiem wcześniejszych testów moda; Windows 11 i Server 2022 nie zostały sprawdzone. Wariant Server Core nie jest wspieranym celem testowym.
- StellarDrive Demo 0.8.2 / Unity 6000.3.24f1 Mono. SHA-256 `Assembly-CSharp.dll`: `25D3F704C52EF689EA04E1EDDDCDB014914D8CBCE071F88871066A6B1375715B`. Inna kompilacja jest blokowana.
- MelonLoader 0.7.3, referencje z `net35`; sam projekt moda jest kompilowany dla .NET Framework 4.7.2 zgodnie z lokalną grą.
- Konto Steam z dostępem do Demo. Natywne sprawdzanie uprawnień pozostaje aktywne. Użyj osobnego konta hosta; równoczesnego grania na tym samym koncie nie zweryfikowano. Nie ma trybu bez Steam.
- W teście pełnego świata z 39 modami proces wykorzystał około 4,13 GiB pamięci prywatnej; to obserwacja, nie gwarantowane minimum. Trzeba zostawić zapas dla Windows, Steam i świata.
- Pliki programu, pobrania i kopie transakcji są na dysku gry. Samodzielne EXE mogą też rozpakować biblioteki środowiska do katalogu tymczasowego użytkownika. Pełny dysk systemowy może uniemożliwić działanie Windows/Steam/instalatora; instalator nie przenosi systemowych katalogów.
- Gra uruchamiana jest z `-batchmode -nographics`, bez okna gry. Silnik nadal może zgłaszać błędy renderowania/compute; są w logach. Nie oznacza to pełnej zgodności wszystkich modów.

## Steam i dołączanie

Analiza tej wersji wykazała **Steam Rich Presence + FishySteamworks/Multipass**, a nie lobby SteamMatchmaking. AutoHost uruchamia natywny serwer, publikuje `connect` i ładuje świat z callbacku sieciowego. Nie tworzymy fikcyjnego ustawienia lobby ani transportu IP.

Znajomy ma próbować dołączenia z listy znajomych Steam do konta VPS. Dokładny przebieg z drugim kontem musi zostać potwierdzony na VPS. Nie twierdzimy, że UDP 27863 lub `--bind`/`--port` konfigurują ten transport. Instalator nie otwiera portów zapory. Steam musi mieć dostęp do swojej infrastruktury; restrykcyjną sieć należy sprawdzić w teście połączenia.

Klienci powinni mieć identyczne wersje modów dodających części i ich zależności. AutoHost ma być na serwerze, nie jest wymaganym protokołem klienta. Zgodność zdalnego klienta nadal wymaga testu. Nie osadzamy 39 cudzych DLL ani gry w EXE. Instalator pobiera dodatki bezpośrednio z przypiętych źródeł autorów. GameMode korzystający z flagi lokalnego hosta może wymagać dodatkowej integracji dla komend administracyjnych.

## Panel, konfiguracja i zapis

Panel ma kontrolę kompletności 39 modów, Start, Stop z zapisem, Restart, log nadzorcy i gry, status Steam/serwera, liczbę graczy, wybór świata, limit graczy, tylko znajomych, autozapis 20–3600 s i listę DLL. Zapis ustawień wymaga zatrzymania serwera. Ostatni wybrany świat jest przechowywany w `StellarDriveServer\autohost.json`.

Świat zapisuje natywny `GameSaver`. Zatrzymane są etapy aktualizacji fizyki świata, kiedy nie ma graczy. Wcześniejszy test pełnego świata potwierdził brak zmiany zegara i stanu podczas pustej sesji oraz odtworzenie po restarcie. Nie jest to test wszystkich własnych timerów każdego dodatku. Szczegóły i hash DLL: [raport moda](Payload/REPORT.md), [dowody](Payload/evidence.json).

Nadzorca ponawia nieoczekiwane awarie do 3 razy w 15 minut, z opóźnieniami. Błąd konfiguracji zatrzymuje pętlę. Nie wymusza zamknięcia zawieszonego procesu. `supervisor.stop` pozostawiony po planowym zatrzymaniu blokuje automatyczny start; przycisk START archiwizuje go. Awaria zasilania lub wymuszone zamknięcie Windows nie gwarantuje końcowego zapisu.

## Autostart

Przycisk w panelu tworzy zadanie **po zalogowaniu konkretnego użytkownika Windows**, z opóźnieniem 60 s. Nie tworzy usługi działającej przed logowaniem. Steam musi już działać i być zalogowany. Nie konfigurujemy automatycznego logowania ani nie zapisujemy hasła. Sam restart VPS bez przywrócenia sesji użytkownika/Steam nie wystarczy. Test restartu Windows pozostaje niewykonany.

## Aktualizacja i odinstalowanie

Zatrzymaj serwer, w panelu wybierz instalator nowego wydania z pliku. Panel się zamknie. Instalator zachowuje istniejącą konfigurację i nie nadpisuje obcych lub zmodyfikowanych poza nim plików. Ten sam mechanizm działa po samodzielnym uruchomieniu nowego Setup.

Odinstaluj z panelu lub wpisu Windows „StellarDrive Dedicated AutoHost”. Usuwane są tylko zarejestrowane, niezmienione pliki programu. Zostają światy, konfiguracja, logi, kopie, loader, inne mody, pliki zmodyfikowane przez użytkownika oraz sam Setup (uruchomionego EXE nie usuwamy). Podczas instalacji pozostają również pliki robocze w `InstallWork`; można je zarchiwizować po zamknięciu instalatora. Identyczny plik moda obecny przed instalacją pozostaje własnością użytkownika i nie jest usuwany.

## Stan testów

Aktualne testy warstwy instalacji: 16 asercji operacji na plikach, w tym rollback po wstrzykniętym błędzie, aktualizacja, zachowanie konfiguracji/świata/cudzego moda, odmowa kolizji i ochrona ścieżek. Fixture to kopie plików, nie uruchomiona gra.

Po prośbie użytkownika nie wykonujemy dalszych uruchomień na jego PC. Kreator został otwarty i formularz odczytany, ale kliknięcia i zrzut okna nie działały w narzędziu sterowania. **Nie uznajemy pełnej instalacji GUI za przetestowaną.** Nie testowano instalacji na czystym VPS, dołączenia drugiego gracza, restartu Windows, pracy 24/7 ani nowego panelu z aktywnym serwerem. Przed produkcją wykonaj [procedurę VPS](Payload/TEST-NA-VPS.md).

## Źródła i CI

`Core/` — transakcje i sterowanie; `Setup/` — WPF kreator; `Manager/` — WPF panel; `Mod/` — rzeczywisty kod AutoHost; `Payload/` — zweryfikowany własny DLL, skrypty i dokumenty; `Tests/` — testy plików.

`build.ps1` buduje gotowe EXE. GitHub Actions robi to na Windows i publikuje prerelease po zmianie `release-version.txt` na gałęzi `main`. Używa dołączonego własnego DLL o przypiętej sumie, nie plików gry. Osobny workflow ręczny buduje mod na własnym runnerze z legalną instalacją wskazaną zmienną `STELLARDRIVE_GAME_DIR`. Nowy DLL wymaga nowej wersji i nowych dowodów/testów przed podmianą w Payload.

Inspiracja adaptacją zapisu headless: [EndlessSkys/StellarDrive-DedicatedServer](https://github.com/3ndl3ssSkys/StellarDrive-DedicatedServer), licencja MIT dołączona. Nie jest to deklaracja wymagań ani działania tamtego projektu na naszym VPS.



## Pełna paczka serwera od 0.3.0

Instalator instaluje Community 1.2.0 (39 modów, w tym zależności) oraz osobno AutoHost. Podsumowanie przed Instaluj pokazuje wszystkie nazwy i wersje. Pobrania, staging, logi i kopie modpacka są w <gra>\StellarDriveCommunity, na dysku gry. Błędne pobranie lub kolizja nie jest ignorowana. Etap Community ma własny rollback. Jeśli późniejsza instalacja panelu nie powiedzie się, zakończony etap Community pozostaje; można ponowić instalację. Odinstalowanie panelu/AutoHost zachowuje Community oraz światy.

Start w panelu i skrypt nadzorcy sprawdzają wymagane pliki wszystkich 39 modów. Brak lub inna wersja blokuje start i wskazuje błąd. Nie uruchamiaj StellarDrive.exe ręcznie: kliknij START w Server Managerze, aby przekazać --autohost-config i parametry headless. Komunikat 'AutoHost nieaktywny: brak --autohost-config' oznacza uruchomienie bez wymaganej konfiguracji.

Kontrole CI używają sztucznych plików testowych i nie uruchamiają gry. Obejmują manifest 39 modów, zależności, archiwa, rollback, ponowną instalację i ochronę cudzych plików. Nie zastępują testu pełnego pobrania/instalacji na VPS ani multiplayera.
