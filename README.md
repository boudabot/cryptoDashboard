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

Pour sauvegarder: copier ce fichier SQLite quand l'application est fermee.

Pour restaurer: fermer l'application, remplacer ce fichier par la sauvegarde, relancer.

## Branche

Branche de travail:

```text
codex/wpf-v01-foundation
```

## Portee V0.1

- fenetre Windows native WPF
- ajout manuel BUY/SELL
- journal SQLite local
- calcul ledger-first: quantite, prix moyen, cout investi, frais, PnL realise
- pas de web, pas d'Electron, pas de Vite, pas de localhost

## Prochain jalon

Valider le ressenti produit WPF. Si la trajectoire convainc, archiver l'ancien prototype Electron et garder WPF comme unique flux principal.
