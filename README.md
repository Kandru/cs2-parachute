# CounterstrikeSharp - Parachute

[![UpdateManager Compatible](https://img.shields.io/badge/CS2-UpdateManager-darkgreen)](https://github.com/Kandru/cs2-update-manager/)
[![GitHub release](https://img.shields.io/github/release/Kandru/cs2-parachute?include_prereleases=&sort=semver&color=blue)](https://github.com/Kandru/cs2-parachute/releases/)
[![License](https://img.shields.io/badge/License-GPLv3-blue)](#license)
[![issues - cs2-map-modifier](https://img.shields.io/github/issues/Kandru/cs2-parachute)](https://github.com/Kandru/cs2-parachute/issues)
[![](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=C2AVYKGVP9TRG)

The Parachute Plugin for CounterstrikeSharp allows players to deploy parachutes during gameplay, adding a new dynamic to the game. This plugin enhances the gaming experience by providing players with more mobility and strategic options while being very customizable.

## Key Features

- **Easy Usage**: Players can deploy parachutes by pressing the Use-Button (defaults to E).
- **Customizable Settings**: Server admins can configure parachute behavior, including fall speed.
- **Compatibility**: Works seamlessly with other CounterstrikeSharp plugins.
- **Lightweight**: Minimal impact on server performance.
- **Open Source**: Fully open-source and available on GitHub for community contributions.

## Installation

1. Download and extract the latest release from the [GitHub releases page](https://github.com/Kandru/cs2-parachute/releases/).
2. Move the "Parachute" folder to the `/addons/counterstrikesharp/plugins/` directory.
3. Restart the server.

Updating is even easier: simply overwrite all plugin files and they will be reloaded automatically. To automate updates please use our [CS2 Update Manager](https://github.com/Kandru/cs2-update-manager/).


## Configuration

This plugin automatically creates a readable JSON configuration file. This configuration file can be found in `/addons/counterstrikesharp/configs/plugins/Parachute/Parachute.json`.

```json
{
  "Enabled": true,
  "FallSpeed": 20,
  "RoundStartDelay": 10,
  "DisableOnRoundEnd": false,
  "DisableWhenCarryingHostage": true,
  "ParachuteModel": "",
  "EnableTeamColors": false,
  "ConfigVersion": 1
}
```

### Enabled

Wether or not the parachute is enabled globally.

### FallSpeed

The speed the player falls when using the parachute.

### RoundStartDelay

Delay after the round starts before the parachute can be used.

### DisableOnRoundEnd

Whether or not to disable the parachute after the round ended.

### DisableWhenCarryingHostage

Whether or not do disable the parachute if a player carries a hostage.

### ParachuteModel

The original parachute model is gone from CS2 since the last update. Therefore specify a custom model or live without one. Empty for no model.

### ParachuteSound

Sound of the parachute itself. Must be a name provided via the soundevents_addon.vsndevts file. Empty for no sound.

### EnableTeamColors

Whether or not the model should have team colors applied.

## Commands

### parachute (Server Console Only)

Ability to run sub-commands:

#### reload

Reloads the configuration.

#### disable

Disables the parachute on next round and remembers this state.

#### enable

Enables the parachute on next round and remembers this state.

## Compile Yourself

Clone the project:

```bash
git clone https://github.com/Kandru/cs2-parachute.git
```

Go to the project directory

```bash
  cd cs2-parachute
```

Install dependencies

```bash
  dotnet restore
```

Build debug files (to use on a development game server)

```bash
  dotnet build
```

Build release files (to use on a production game server)

```bash
  dotnet publish
```

## FAQ

TODO

## License

Released under [GPLv3](/LICENSE) by [@Kandru](https://github.com/Kandru).

## Authors

- [@derkalle4](https://www.github.com/derkalle4)
- [@jmgraeffe](https://www.github.com/jmgraeffe)
