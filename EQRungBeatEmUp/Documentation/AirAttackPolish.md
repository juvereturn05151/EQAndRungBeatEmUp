Both playable characters use slightly slower authored air timelines at the existing 60 Hz combat clock.

| Attack | Previous frames | New frames |
| --- | ---: | ---: |
| Air combo 1 | 23 | 27 |
| Air combo 2 | 24 | 28 |
| Air combo finisher | 36 | 42 |
| Air dive | 24 | 28 |

The timeline expands by approximately 15%, including startup, active pose holds,
cancel windows and recovery. Added holds retain hit IDs and gravity but do not
repeat frame-entry cues, vertical impulses or movement displacements. Dive
landing and airborne-hold indices move to 17 and 16. Physics still run at 60 Hz.

Both third air-combo hits use the existing yellow Cartoon FX impact burst at
0.24 scale and 0.6-second lifetime. Confirmed enemy hits create the effect at the
contact point above the character sprites; misses and rejected hits do not.
Damage, hitstop, juggle and downward ground-bounce settings are retained.

The editor command `Beat Em Up/Combat/Apply slower air attacks and finisher impact`
is idempotent for stamped assets. It also refreshes the multiplayer catalog.
Play Mode checks cover both characters' combos, cancels, landings, both facing
directions, duplicate-hit protection, and confirmed-hit/miss feedback.
