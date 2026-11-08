# Raport — StellarDrive AutoHost 0.1.2-alpha.1

Data: 2026-11-08. Stan: **eksperymentalny prototyp do testu na VPS, nie ukończony serwer produkcyjny 24/7**.

DLL SHA-256: `3b794ef574676fc4a71e79c21716a32d2a10f86d8932668244ebdcae6d4bd19e`.

## Faktycznie zaimplementowano

- Samoczynne oczekiwanie na Steam, wejście do MainGame, uruchomienie wyłącznie serwerowej strony natywnego FishySteamworks przez Multipass.
- Wczytanie wskazanego `world.json` przez natywny ServerStateLoader. Nie ma sztucznego gracza-hostującego.
- Ogłoszenie dołączania przez Steam Rich Presence. Gra nie używa SteamMatchmaking lobby do tej ścieżki; nie dodano martwego lobby.
- Opcjonalna kontrola znajomych na podstawie SteamID połączenia transportowego, nie nazwy podanej w czacie. Funkcja wymaga testu z dwoma kontami.
- Natywny autozapis, kopia lastgood, pominięcie miniatury GPU, kontrola zmiany pliku i parsowania zapisanego JSON, blokada otwarcia świata przez drugi AutoHost.
- Polecenia `save` i `stop`, archiwum przetworzonych poleceń, logi i status.
- Pauza etapów fizyki pustego świata. Nie wyłączono modów.
- Eksperymentalna poprawka wyboru materiałów `_Albedo` tylko w Odyssey Building/Power przy grafice Null. Oryginalne DLL nie zostały przebudowane ani zmienione.
- Nadzorca PowerShell z ograniczonymi ponowieniami oraz skrypt rejestracji zadania po logowaniu. Zadania nie zarejestrowano na komputerze użytkownika ani na VPS.

## Testy wykonane lokalnie

Środowisko: Windows 10 x64, Unity 6000.3.24f1 Mono, StellarDrive Demo 0.8.2, MelonLoader 0.7.3 net35. Działający Steam w tej samej sesji Windows. Oddzielne kopie gry i zapisów.

| Sprawdzenie | Wynik i zakres |
|---|---|
| Kompilacja Release | 0 błędów, 0 ostrzeżeń. Osobna kompilacja źródeł z katalogu dystrybucji dała identyczny SHA-256 DLL. |
| Start bez dodatkowych modów rozgrywki | Steam zainicjalizowany, FishNet serwer Started, świat załadowany, localClient=false, zero graczy. |
| Brak grafiki | `graphics: Null`, uruchomienie `-batchmode -nographics`; dla pełnej paczki MainWindowHandle=0. Skrypt ukrywa również konsolę/startscreen loadera. |
| Ogłoszenie Steam | SetRichPresence zwróciło true. **Nie jest to test widoczności u znajomego ani jego dołączenia.** |
| Autozapis i planowane zamknięcie | Potwierdzone zapisy na dysku, poprawny JSON, zakończenie procesów oraz kod 0 nadzorcy po Stop. |
| Pełna paczka | 39 istniejących modów + AutoHost = 40 załadowanych modów. 434 zarejestrowane części, w tym Heavy Gyroscope 248 i Hydrogen Thruster 201/228/229/230. To test inicjalizacji, nie wszystkich funkcji w rozgrywce. |
| Pusty pełny świat | Dwa zapisy oddalone o ok. 25,93 s: taki sam stan świata, czas 25958.35285278727 bez zmiany, zero graczy, brak lokalnego klienta. |
| Restart pełnego świata | Ponowne wczytanie całego katalogu świata, autozapis i porównanie stanu: zaliczone. 80 kwaternionów porównano z uwzględnieniem równoważności q/-q; pominięto wyłącznie pochodne eulerAngles w kwaternionach i znacznik czasu modsUsed/stampedUtc. Pozostałe wartości identyczne. |
| Dodatkowe pliki świata | 7 sprawdzonych plików dodatków zachowało SHA-256, m.in. Arsenal, SDModKit, SafetyRails i TF Advanced. |
| Awaria i restart nadzorcy | Wymuszono zakończenie wyłącznie procesu izolowanej gry testowej. Nadzorca uruchomił ją ponownie po 10 s. Druga awaria przy MaxRestarts=1 zakończyła nadzorcę błędem limitu, zamiast tworzyć nieskończoną pętlę. |
| Integralność oryginałów | 127 oryginalnych plików zapisów bez zmian względem kopii sprzed testów. Wszystkie 39 oryginalnych DLL zgodne z użytymi w laboratorium. AutoHost nie zainstalowano w głównej grze. |

Pamięć prywatna procesu pełnego świata w próbce: 4 429 742 080 bajtów (około 4,13 GiB). To nie minimalne wymaganie RAM i nie jest równoznaczne z fizycznym zestawem roboczym. Pomiar około 341 MB dla procesu w menu nie opisuje załadowanej paczki.

Pierwsza próba pełnej paczki kopiowała tylko world.json. TF Advanced przebudował wtedy starsze stacje z powodu braku towarzyszącego pliku wersji. Tej próby nie użyto jako dowodu odtworzenia pełnego świata. Test końcowy skopiował cały katalog i zachował komplet stanu. Zawsze przenoś cały folder świata.

## Nierozstrzygnięte i znane ograniczenia

1. Nie ma dostępu do Waszego VPS ani niezależnego drugiego konta Steam. Nie wykonano testu Windows Server 2022, widoczności/dołączenia znajomego, odrzucenia obcego konta, synchronizacji gry między klientami, wejścia po pustej sesji ani Steam przez sieć dostawcy VPS. Poprzednie testy innego prototypu przez lokalne IP nie zastępują tych testów.
2. Nie wykonano próby 24-godzinnej. Skrypt restartuje zakończony proces; nie wykrywa wszystkich zawieszeń i nie zabija automatycznie procesu w trakcie zapisu.
3. Silnik nadal zgłasza błędy dotyczące obliczeń/renderowania bez GPU, m.in. `Kernel index (0) out of range` i `EndRenderPass: Not inside a Renderpass`. Są rejestrowane w logach. Pomimo nich lokalnie załadowały się części i świat oraz działał zapis; nie oznacza to pełnej zgodności każdej funkcji modów.
4. Start po restarcie Windows zależy od zalogowanej sesji Steam. Dostarczone zadanie startuje po logowaniu użytkownika, nie przed nim. Nie skonfigurowano automatycznego logowania, usługi systemowej ani danych dostępowych.
5. Ponieważ nie istnieje lokalny gracz-host, funkcje GameMode wymagające jego flagi nie otrzymują automatycznie administratora. Suwak GameMode nie steruje interwałem AutoHost. To rzeczywiste ograniczenia zgodności wymagające dalszej integracji, jeśli te funkcje mają być dostępne na serwerze.
6. Brak testów pełnego dysku i utraty zasilania. Natywny zapis plus sidecary różnych modów nie stanowią atomowej transakcji. Blokada chroni przed drugim AutoHost, nie przed zwykłą grą otwierającą ten sam folder.

## Dostarczone pliki i dalszy test

Paczka zawiera produkcyjną DLL bez harnessu testowego, kod, konfigurację, skrypty start/stop/rejestracji zadania, instrukcję, changelog i raport powiązany z DLL przez SHA-256. Nie zawiera gry, zapisów, prywatnych logów ani 39 cudzych DLL.

Instrukcja `TEST-NA-VPS.md` opisuje brakujące testy odbiorcze. Status zadania pozostaje nieukończony w zakresie rzeczywistej sesji Steam z innym graczem i pracy na VPS. Nie opublikowano prototypu jako gotowego/stabilnego moda na publicznej stronie i nie wykonano wdrożenia.

Sprzęt estradowy jest osobnym projektem w toku. Pierwszy test Stage potwierdził tworzenie urządzeń przez natywne komunikaty, okablowanie, wyciszanie, sterowanie światłem i zużycie płynu, ale zawiódł test emisji cząstek; odtwarzanie audio również wymaga dalszej naprawy/weryfikacji. Stage nie jest częścią tej paczki i nie został uznany za ukończony.

Odniesienia: [Steam Rich Presence](https://partner.steamgames.com/doc/api/ISteamFriends#SetRichPresence), [Steam initialization](https://partner.steamgames.com/doc/api/steam_api#SteamAPI_Init), [Unity headless](https://docs.unity3d.com/6000.0/Documentation/Manual/desktop-headless-mode.html), [MelonLoader launch options](https://github.com/LavaGang/MelonLoader), [wcześniej analizowany projekt EndlessSkys](https://github.com/3ndl3ssSkys/StellarDrive-DedicatedServer).
