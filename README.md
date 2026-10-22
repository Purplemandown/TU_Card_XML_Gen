# TU_Card_XML_Gen

Takes a plain-text **update file** describing new card stats and skills, and writes those changes into the Tyrant Unleashed card XMLs for you, including every fusion tier and every upgrade level, with stats scaled down automatically.

Current version: **1.6.0**. Requires the .NET 8 runtime.

- [Setup](#setup)
- [Update file format](#update-file-format)
- [Examples](#examples)
- [Skill syntax](#skill-syntax)
- [Triggers](#triggers)
- [Shortcuts](#shortcuts)
- [How scaling works](#how-scaling-works)
- [Mass nerf poison](#mass-nerf-poison)
- [Gotchas](#gotchas)
- [Changes in 1.6.0](#changes-in-160)

## Setup

Edit `config.xml` (it sits next to the exe), then run the exe.

| Setting | Meaning |
|---|---|
| `<Operation>` | `standard` applies the update file. `massnerfpoison` multiplies Poison on every card by a factor. |
| `<PathToCardXMLs>` | Folder holding the game XMLs. It must contain `cards_section_1.xml`, `cards_section_2.xml`, … (numbered from 1 with no gaps), `skills_set.xml` and `fusion_recipes_cj2.xml`. |
| `<PathToUpdateFile>` | Full path to your update file (standard operation only). |
| `<AutoStats>` | `PercentAcrossTier` (default 15) and `PercentBetweenTiers` (default 5), used by autostats scaling. See [scaling](#how-scaling-works). |
| `<Scaling>` | `<Mode>`: `autostats` (default) or `tiered`. `<Tiered>`: `FusionLevel0` (default `50..60`), `FusionLevel1` (`60..80`), `FusionLevel2` (`80..100`), `Summoned` (`50..100`). See [scaling](#how-scaling-works). |
| `<MassNerfPoisonConfig><Factor>` | Default `.4` (massnerfpoison only). |

> **The card XMLs are changed in place.** The tool overwrites the `cards_section_N.xml` files that contain a changed card. If anything goes wrong, reset the XMLs by downloading them again in TUO.

The tool also creates an `output-<date-time>` folder next to the exe with one small XML per updated card (`<card name>.xml`), handy for review and for the update you send to Kongregate.

At the end the console lists any cards that **failed to update** and any **warnings**. Read it.

## Update file format

Each card is a block of lines:

| Line | Content |
|---|---|
| 1 | Card name |
| 2 | Rarity / faction. Required as a placeholder, but **ignored** by the tool. |
| 3 | Stats |
| 4+ | Skills, one per line |

**Between cards** put one or more blank lines and/or `---` lines. Any mix of blank, `---` and comment lines between two cards counts as a single gap.

**`---`** (three or more dashes) means *the next card is summoned by the card before it*. The tool checks this and prints a `WARNING` if the card before the `---` doesn't have a Summon for the card after it. Both cards are still updated either way.

**Comments** start with `//` and run to the end of the line. They can be on their own line or after anything else. A comment line inside a card doesn't split the card.

**`// SCALE:` comments** change the scaling for every card that follows, e.g. `// SCALE: 50..60, 60..80, 80..100`, `// SCALE: 50..100` or `// SCALE: DEFAULT`. See [SCALE comments](#scale-comments).

Windows (CRLF) and Unix (LF) line endings both work.

### Card name (line 1)

- Must match the in-game name **exactly**, including capitalisation and spacing.
- Use the name of the **highest fusion tier** you want changed. The tool walks the fusion recipes backwards and updates every lower tier for you. Commanders are followed through their ascension recipes too (Neocyte Fusion Core, Neocyte Core, Altar).
- The old `Name1|Name2|Name3` pipe syntax is gone.

### Stats (line 3)

These are the **max** stats. See [scaling](#how-scaling-works) for how lower levels and tiers are worked out.

| Format | Used for | Example |
|---|---|---|
| `attack/health/delay` | Assaults | `60/200/4` |
| `health/delay` | Structures, commanders | `800/2`, `1200/-` |
| `<health>HP` | Commanders | `1200HP` (same as `1200/-`) |

- Use `-` for delay to leave the existing delay alone: `60/200/-`.
- Delay is only written to cards that already have one, and attack only to cards that already have one, so commanders won't suddenly grow an attack stat.

### Skills (line 4+)

- The skills you list **replace all existing skills** on the card, on every tier and level. List every skill the card should have, not just the changed ones.
- Skills are written to the XML in the order you list them.
- Values are the max values. Lower levels and tiers scale down.
- **Nothing is case-sensitive**: skill names, `all`, `every`, factions and triggers can be written in any case (`RALLY ALL rt 100 On Attacked` is fine).
- A word the tool doesn't recognise (e.g. a misspelt skill) makes that card **fail** with a message naming the word, rather than writing bad XML.

## Examples

Some card names below are made up.

**Assaults, plus a summon pair**

```text
// Righteous rework, Oct 2026
Pure Benediction            // top tier; lower tiers are found automatically
Legendary Righteous
60/200/4
Evade 20
Rally all Righteous 100 attacked
Mortar all 60

Utopia Prayer
Mythic Righteous
20/200/3
On Play: Jam all every 4
Bravery 50
Refresh 60

Hive Mother
Epic Xeno
50/100/2
Summon Hive Drone play
---
Hive Drone                  // summoned by Hive Mother
Epic Xeno
10/20/1
Strike 5
```

**Commander** (health only, no attack, no delay)

```text
Malika Ascended
Legendary Imperial
1200HP
Heal all Imperial 50
Enrage 2 Xeno 30 death
```

**Structure** (health/delay, no attack)

```text
Bulwark Tower
Epic Raider
800/2
Armor 30
Counter 20
```

## Skill syntax

`<value>` is the skill amount, `<n>` the number of targets, `<cd>` the cooldown ("every" turns), `<faction>` one of Imperial, Raider, Bloodthirsty, Xeno, Righteous, Progenitor. `[ ]` means optional.

| Skills | Syntax | Examples |
|---|---|---|
| Wall, Rush | *(no parameters)* | `Wall` |
| Armor, Counter, Pierce, Poison, Leech, Evade, Berserk, Corrosive, Inhibit, Valor, Legion, Payback, Avenge, Refresh, Venom, Mend, Swipe, Rupture, Allegiance, Drain, Stasis, Revenge, Coalition, Sabotage, Barrier, Subdue, Tribute, Bravery, Absorb, Disease, Mark, Fortify, Hunt, Scavenge | `<skill> <value>` | `Armor 40` |
| Flurry, Mimic | `<skill> [<value>] [every <cd>]` | `Flurry 2 every 4`, `Flurry every 6`, `Mimic 50` |
| Strike, Weaken, Siege, Mortar, Enfeeble, Sunder | `<skill> [all] <value>` | `Strike all 50`, `Weaken 30` |
| Heal, Rally, Protect | `<skill> [all] [<faction>] <value>` | `Rally all Righteous 100`, `Heal 20` |
| Jam | `Jam <n or all>` / `Jam every <cd>` / `Jam <n or all> every <cd>` | `Jam 2`, `Jam every 4`, `Jam all every 5` |
| Overload | `Overload <n or all> [<faction>]` | `Overload all`, `Overload 2 Xeno` |
| Enrage, Entrap | `<skill> <value>` / `<skill> <all, n or faction> <value>` / `<skill> <all or n> <faction> <value>` | `Enrage 30`, `Enrage Xeno 30`, `Entrap 2 Raider 40` |
| Enhance | `Enhance [<n or all>] <skill> <value>` | `Enhance Armor 20`, `Enhance all Counter 15` |
| Evolve | `Evolve [<n or all>] <skill> [to] <skill>` | `Evolve Armor to Counter`, `Evolve all Armor into Counter`, `Evolve 2 Armor Counter` |
| Summon | `Summon <card name>` | `Summon Hive Drone play` |

Notes:

- **`Jam 4` means 4 targets, not a cooldown.** Use `Jam every 4` for the cooldown.
- **Summon** looks the card up by name (any case). Lower tiers and levels of the summoner get a lower upgrade level of the summoned card; the top gets the max.

## Triggers

A trigger can go at the **end or the start** of a skill line, in any case:

| End | Start |
|---|---|
| `Jam all every 4 play` | `On Play: Jam all every 4` |
| `Rally all RT 100 OnAttacked` | `on attacked Rally all RT 100` |
| `Enrage 2 Xeno 30 On Death` | `Death: Enrage 2 Xeno 30` |

Recognised words are `play`, `attacked` and `death`, optionally preceded by `On` or `On ` and optionally followed by a colon.

`Summon Angel of Death` summons *Angel of Death* (with no trigger) if that card exists, rather than treating "Death" as a trigger.

## Shortcuts

| Shortcut | Means |
|---|---|
| `XN` | Xeno |
| `Imp` | Imperial |
| `RD` | Raider |
| `BT` | Bloodthirsty |
| `RT` | Righteous |
| `Prog` | Progenitor |
| `Zerk` | Berserk |

Use the skill names you see in game (Armor, Mortar, …). They're converted to their XML ids (`armored`, `besiege`, …) using `skills_set.xml`. Typing the XML id works too.

## How scaling works

The values you type are the **max** values. Lower levels and lower fusion tiers get a percentage of them. There are two ways to work out that percentage, chosen by `<Scaling><Mode>` in `config.xml` and by [SCALE comments](#scale-comments) in the update file.

For both:

- Applies to attack, health, skill values and target counts (`n`).
- **A max of 0 stays 0** at every level (e.g. 0 attack, `Rally 0`).
- Anything else is rounded (see below), with a minimum of 1.
- **Not** scaled: delay, cooldowns, factions, `all`, and the skills chosen for Enhance/Evolve.
- The console prints which scaling each card used.
- If you need exact values at specific levels, run the tool, then hand-edit those levels in the output.

### Autostats (the original behaviour)

Counted down from the top tier using `<AutoStats>`. With the defaults (15 across, 5 between), for a card with three fusion tiers:

| Tier | Max level | Level 1 |
|---|---|---|
| 3 | 100% | 85% |
| 2 | 80% | 65% |
| 1 | 60% | 45% |

Results round **down**.

### Tiered

Used when `<Mode>` is `tiered`, or after any SCALE comment other than `DEFAULT`. Each fusion level has its own range: level 1 of the tier gets the first number, the tier's max level gets the second, and the levels between are spread evenly.

| Card | Default range (% of max) |
|---|---|
| Fusion level 0 (base card) | 50 – 60 |
| Fusion level 1 | 60 – 80 |
| Fusion level 2 | 80 – 100 |
| Summoned card (every tier) | 50 – 100 |

- Fusion levels count **up from the base card**: level 0 is the card with no fusion recipe, level 1 is made from it, and so on. Fusion levels above the last range use the last range.
- A **summoned card** is one that comes right after a `---` in the update file, or one in set 9500.
- Results round to the **nearest** whole number (halves round up).

Example: Pure Benediction with 200 health and 3 levels per tier.

| Card | Level 1 | Level 2 | Level 3 |
|---|---|---|---|
| Benediction | 100 | 110 | 120 |
| Holy Benediction | 120 | 140 | 160 |
| Pure Benediction | 160 | 180 | 200 |

### SCALE comments

A comment starting with `SCALE:` changes the scaling for every card that starts after it, until the next SCALE comment. If it's on a card's name line, that card is included. Not case-sensitive; `%` signs are optional.

| Comment | Effect |
|---|---|
| `// SCALE: 50..60, 60..80, 80..100` | Tiered. Fusion levels 0, 1, 2 use these ranges; summoned cards keep the `Summoned` range from `config.xml`. |
| `// SCALE: 50..100` | Tiered. One range for every card, summoned cards included. |
| `// SCALE: 50..60, 60..80, 80..100, SUMMON 50..100` | Tiered, and also sets the summoned-card range. |
| `// SCALE: DEFAULT` | Back to whatever `config.xml` says. |

A SCALE comment the tool can't read stops the whole run **before anything is changed**, and says which line is wrong.

## Mass nerf poison

Set `<Operation>massnerfpoison</Operation>`. Every card whose max level has Poison gets Poison multiplied by `<Factor>` on every level, rounded **up**. The update file isn't used. The XMLs are changed in place, as above.

## Gotchas

- Line 2 must exist even though it's ignored; skipping it makes the tool read your first skill as stats.
- Card names (line 1) are matched exactly and the first match wins, so a typo means *"Can't find a card with name …"* and that card is skipped.
- A card can only be updated once per run. If a later card shares fusion tiers with an earlier one (e.g. Pure Benediction, then Holy Benediction), the later one fails without changing anything. Do them in separate runs.
- `Jam 4` is four targets, not "every 4".

## Changes in 1.6.0

- Comments: `//` to end of line.
- Cards can be separated by any number of blank, comment and `---` lines.
- `---` marks a summoned card, with a warning if the card before doesn't summon it.
- Skills, factions, keywords and triggers are no longer case-sensitive.
- Triggers can be written at the start or end of a skill line.
- Evolve works (it couldn't be parsed before); `to`/`into` are optional.
- Commander health can be written as `1200HP`.
- Unknown skills or words fail the card with a clear message, instead of writing bad XML or silently dropping a trigger.
- Fixed `Prog` turning `Progenitor` into `Progenitorenitor`.
- Summon card names containing a skill name (e.g. "… Mortar …") now work.
- Windows and Unix line endings both work in the update file.
- New **tiered scaling** option (`config.xml` `<Scaling>`), with per-fusion-level and summoned-card ranges.
- `// SCALE:` comments change the scaling for the cards that follow.
- A max value of 0 stays 0 at every level (it used to become 1).
- A card that shares fusion tiers with an earlier card in the same run now fails cleanly; before, the shared tiers were left half-changed.
