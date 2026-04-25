# Build Windows

Le build genere un executable Windows autonome. L'usage final reste un double-clic sur `localCrypto.exe`.

## Script recommande

Depuis la racine du repo:

```bat
build-current.bat
```

Le script detecte la branche Git active et choisit un dossier de sortie explicite.

## Dossiers de sortie

| Branche active | Dossier genere |
| --- | --- |
| `master` | `release\localCrypto` |
| `dev` | `release\localCrypto-dev` |
| `codex/portfolio-truth-upgrade` | `release\localCrypto-codex-portfolio-truth-upgrade` |
| autre branche | `release\localCrypto-<nom-branche>` |

`release\localCrypto` est reserve a la version stable issue de `master`.

## Regles

- Toujours verifier la branche avant un build important.
- Ne pas lancer deux builds vers le meme dossier en meme temps.
- Si `localCrypto.exe` est ouvert, le build du meme dossier peut echouer car Windows verrouille le fichier.
- Les dossiers `release/` sont ignores par Git.

## Donnees locales

Les builds utilisent la meme source de verite par defaut:

```text
%APPDATA%\localCrypto\localcrypto.sqlite
```

Pour tester une ecriture d'import risquee, faire une sauvegarde SQLite avant validation.
