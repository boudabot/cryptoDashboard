# localCrypto WPF Spike

Application Windows native locale pour tester la trajectoire WPF + C# + SQLite.

## Modes

### Developpement

```powershell
.\run-app.bat
```

Lance l'application WPF avec le SDK .NET. C'est le mode de travail dev.

### Test local

```powershell
.\test-app.bat
```

Lance les tests du domaine portefeuille.

### Application packagee

```powershell
.\build-app.bat
```

Produit l'executable double-clic ici:

```text
release\localCrypto\localCrypto.exe
```

En usage normal, il ne faut pas lancer de terminal, pas de serveur et pas de localhost.

## Donnees

Source de verite unique:

```text
%APPDATA%\localCrypto\localcrypto.sqlite
```

Depuis V0.1.1, l'application expose un bloc `Donnees` pour:

- sauvegarder la base SQLite active
- restaurer une sauvegarde SQLite avec confirmation
- rappeler que l'ancien cache Electron n'est pas la source portefeuille WPF

## Branche

Flux de branches:

```text
master: release stable
dev: integration
codex/...: evolutions
```

Branche de travail actuelle:

```text
codex/v011-data-safety
```

## Portee V0.1.1

- fenetre Windows native WPF
- ajout manuel BUY/SELL
- journal SQLite local
- sauvegarde/restauration SQLite depuis l'application
- suppression transaction avec confirmation
- refus lisible des doublons probables
- calcul ledger-first: quantite, prix moyen, cout investi, frais, PnL realise
- pas de web, pas d'Electron, pas de Vite, pas de localhost

## Prochain jalon

Valider V0.1.1 en usage reel, puis ajouter une premiere preview d'import CSV sans ecrasement silencieux.
