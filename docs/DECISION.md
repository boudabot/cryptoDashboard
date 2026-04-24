# Decision Spike WPF

## Hypothese testee

WPF + C# + SQLite peut produire une application Windows locale plus claire pour l'usage final:

- double-clic
- pas de navigateur
- pas de serveur local
- pas de confusion avec une app web
- packaging Windows normal

## Ce qu'on garde de l'ancien prototype

- logique ledger-first
- SQLite comme source de verite
- priorite transactions, frais, prix moyen, positions, PnL
- scripts simples run/build/test

## Ce qu'on ne reprend pas

- Electron
- React
- Vite
- localhost
- backend separe

## Regle de decision

Cette spike ne doit pas devenir un deuxieme produit durable.

Si WPF convainc, l'ancien prototype Electron est archive et WPF devient la trajectoire unique.

Si WPF ne convainc pas, on abandonne cette spike et on revient a Electron en mode desktop clarifie.
