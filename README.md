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

Depuis V0.1.2, l'application expose:

- sauvegarder la base SQLite active
- restaurer une sauvegarde SQLite avec confirmation
- rappeler que l'ancien cache Electron n'est pas la source portefeuille WPF
- charger un export Binance CSV/XLSX en preview sans ecrire dans SQLite

## Branche

Flux de branches:

```text
master: release stable
dev: integration
codex/...: evolutions
```

Branche de travail actuelle:

```text
codex/binance-import-preview
```

## Portee V0.1.2

- fenetre Windows native WPF
- ajout manuel BUY/SELL
- journal SQLite local
- sauvegarde/restauration SQLite depuis l'application
- suppression transaction avec confirmation
- refus lisible des doublons probables
- preview d'import Binance CSV/XLSX
- classification trades, rewards, mouvements internes, cash et rejets
- calcul ledger-first: quantite, prix moyen, cout investi, frais, PnL realise
- pas de web, pas d'Electron, pas de Vite, pas de localhost

## Prochain jalon

Valider la preview Binance sur exports reels, puis grouper les lignes trade en transactions ledger avant ecriture SQLite.
