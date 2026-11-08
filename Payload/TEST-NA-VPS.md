# Test odbiorczy na VPS — wymagany przed używaniem normalnego świata

Nie przenoś jeszcze jedynej kopii świata. Przygotuj jego pełną kopię, wraz z plikami dodatków.

1. Uruchom z konfiguracją wskazującą kopię. Sprawdź `graphics: Null`, `steamInitialized: true`, `serverStarted: true`, `localClient: false`, `worldLoaded: true` w `status.json`.
2. Przejrzyj `MelonLoader/Latest.log` i `unity-errors.log`. Sprawdź zgodność wersji części z klientem. Samo `ready` nie oznacza, że wszystkie mody działają.
3. Na drugim koncie Steam, będącym znajomym konta VPS, użyj „Dołącz do gry”. Sprawdź załadowanie świata i wzrost `players` do 1. Nie zastępuj tego testem z dwiema instancjami jednego konta.
4. Postaw blok i użyj wybranej maszyny/modowanej części. Drugi gracz powinien zobaczyć zmianę. Zapisz przez `commands.txt` (linia `save`) i poczekaj na wzrost `saves` oraz `saving: false`.
5. Wyjdź wszystkimi klientami. Odczekaj 1–2 minuty. Proces ma pozostać aktywny z `players: 0`; czas świata i fizyka mają być wstrzymane.
6. Wejdź ponownie: blok i ustawienia maszyny muszą pozostać. Zatrzymaj przez skrypt Stop, poczekaj na zakończenie procesu. Przenieś znacznik `supervisor.stop` do archiwum. Uruchom ponownie i ponownie dołącz.
7. Osobno sprawdź konto, które nie jest znajomym. Przy `FriendsOnly: true` połączenie ma być odrzucone. Nie uznawaj tego testu za zaliczony tylko na podstawie ustawienia w JSON.
8. Rozłącz RDP bez wylogowania. Sprawdź, czy klient nadal gra. Następnie w kontrolowanym oknie przetestuj restart VPS i politykę sesji Windows/Steam. Zadanie AtLogOn nie zastępuje logowania.
9. Przeprowadź próbę co najmniej 24 godzin: kilka wejść/wyjść, normalna rozgrywka, zapisy, zużycie RAM i wielkość logów. Wyznacz kopie zapasowe całego katalogu świata poza dyskiem roboczym.

Do diagnozy wystarczą konfiguracja bez prywatnych danych, status i odpowiednie fragmenty logów. Nie udostępniaj haseł Steam, kodów Steam Guard ani tokenów VPS.
