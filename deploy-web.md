# Deploying Merimna to Firebase Hosting

- Firebase project: `merimna-game-6020` (linked in `.firebaserc`)
- Live URL: https://merimna-game-6020.web.app
- Console: https://console.firebase.google.com/project/merimna-game-6020/overview
- What gets uploaded: the folder `Builds/Web` (see `firebase.json`).

## Update the live game
1. In Unity: **Tools > Merimna > Build WebGL** (overwrites `Builds/Web`).
2. Deploy:
```bash
firebase deploy --only hosting --account iriszimmerfrau@gmail.com
```
(`--account` is only needed if the CLI's active login is a different Google account; check with `firebase login:list`.)

## Try it locally first
```bash
firebase emulators:start --only hosting
```
