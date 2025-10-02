> [!TIP]
> You can test this plug-in by joining our community server via `connect server.counterstrike.party:27030` or via https://counterstrike.party

# CounterstrikeSharp - Parachute

[![UpdateManager Compatible](https://img.shields.io/badge/CS2-UpdateManager-darkgreen)](https://github.com/Kandru/cs2-update-manager/)
[![GitHub release](https://img.shields.io/github/release/Kandru/cs2-parachute?include_prereleases=&sort=semver&color=blue)](https://github.com/Kandru/cs2-parachute/releases/)
[![License](https://img.shields.io/badge/License-GPLv3-blue)](#license)
[![issues - cs2-map-modifier](https://img.shields.io/github/issues/Kandru/cs2-parachute)](https://github.com/Kandru/cs2-parachute/issues)
[![](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=C2AVYKGVP9TRG)

The Parachute Plugin for CounterstrikeSharp brings some fun to your CS2 matches by letting players glide around with parachutes! This playful modification adds a silly twist to the game, giving everyone more ways to move around the map and create funny moments.

## What Makes It Fun

- **Simple Controls**: Just hold the Use button (usually E) while falling to deploy your parachute
- **Adjustable Fun**: Server owners can tweak how fast you fall, how much you can steer, and other settings
- **Sound Effects**: Add custom sounds to make your parachuting experience even more entertaining
- **No Performance Issues**: Lightweight design means the fun doesn't slow down your server

## Installation

1. Download and extract the latest release from the [GitHub releases page](https://github.com/Kandru/cs2-parachute/releases/).
2. Move the "Parachute" folder to the `/addons/counterstrikesharp/plugins/` directory.
3. Restart the server.

Updating is even easier: simply overwrite all plugin files and they will be reloaded automatically. To automate updates please use our [CS2 Update Manager](https://github.com/Kandru/cs2-update-manager/).


## Configuration

This plugin automatically creates a readable JSON configuration file. This configuration file can be found in `/addons/counterstrikesharp/configs/plugins/Parachute/Parachute.json`.

```json
{
  "enabled": true,
  "round_start_delay": 10,
  "disable_on_round_end": false,
  "disable_when_carrying_hostage": true,
  "parachute": {
    "is_hoverboard": false,
    "fallspeed": 0.1,
    "sidewards_movement_modifier": 1.0075,
    "hoverboard_movement_modifier": 1.0075,
    "parachute_model": "models/cs2/kandru/hoverboard.vmdl",
    "parachute_model_size": 1,
    "parachute_sound": "Kandru.Hoverboard",
    "parachute_sound_interval": 1.266,
    "enable_team_colors": false
  },
  "ConfigVersion": 1
}
```

### Enabled

Wether or not the parachute is enabled globally.

#### round_start_delay

Delay after the round starts before the parachute can be used.

#### disable_on_round_end

Whether or not to disable the parachute after the round ended.

#### disable_when_carrying_hostage

Whether or not do disable the parachute if a player carries a hostage.

### Parachute

Settings regarding the parachute.

#### fallspeed

The speed the player falls when using the parachute.

#### sidewards_movement_modifier

The sidewards speed increasement modifier.

#### hoverboard_movement_modifier

The sidewards speed increasement modifier if hoverboard.

#### parachute_model

The original parachute model is gone from CS2 since the last update. Therefore specify a custom model or live without one. Empty for no model.

#### parachute_model_size

The size of the parachute.

#### parachute_sound

Sound of the parachute itself. Must be a name provided via the soundevents_addon.vsndevts file. Empty for no sound.

#### parachute_sound_interval

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
