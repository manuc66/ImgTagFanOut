# Jeu de démonstration

17 portraits réels, 6 personnes, pour essayer la détection et les suggestions de
visages d'ImgTagFanOut sans utiliser ses propres photos.

Chaque personne a deux ou trois photos **réellement différentes** (années,
éclairage, pose, noir et blanc), ce qui rend le test honnête : les suggestions ne
se contentent pas de reconnaître un doublon.

Ouvrir ce dossier comme dossier de travail dans l'application, lancer
`Fichier > Analyser les visages`, puis nommer un visage : les autres photos de la
même personne doivent être suggérées.

## Limites

Toutes les personnes sont des présidents américains, donc des hommes âgés en
costume. Ce jeu prouve que la chaîne fonctionne, il ne mesure pas la qualité du
modèle sur des familles, des enfants ou des photos de téléphone.

## Source et licence

Portraits officiels du gouvernement américain, **domaine public** (travaux du
gouvernement fédéral des États-Unis), téléchargés depuis Wikimedia Commons. La
liste exacte des fichiers et leur page Commons sont dans `manifest.json`.

Le script qui a produit ce dossier est `tools/make-demo-portraits.py`.
