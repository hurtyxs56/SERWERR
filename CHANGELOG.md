# 0.2.1-alpha.1

- Naprawiono odczyt skryptów przez Windows PowerShell 5.1: instalator zawiera UTF-8 z BOM.
- Dodano kontrolę BOM i parsowanie wszystkich skryptów w PowerShell 5.1 przed kompilacją instalatora. Test nie uruchamia gry.
- AutoHost DLL pozostaje bez zmian; wymagane testy uruchomienia na VPS.

# 0.2.0-alpha.1

- Dodano self-contained kreator Windows i Server Manager.
- Wykrywanie bibliotek Steam, ręczny wybór gry, zgodność przez SHA-256.
- Transakcje kopiowania, kontrola kolizji, rollback, manifest własności plików.
- Aktualizacja i odinstalowanie z zachowaniem światów i innych modów.
- Panel konfiguracji, logów i sterowania niezależnym nadzorcą PowerShell.
- Opcjonalne zadanie po logowaniu użytkownika Windows.
- AutoHost pozostaje w wersji 0.1.2-alpha.1, z niezmienionym DLL.
- Status alpha: brak testu VPS, drugiego konta Steam i pełnego GUI; nie produkcyjne 24/7.
