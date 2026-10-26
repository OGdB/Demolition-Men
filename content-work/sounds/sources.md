# Action sound sources

The crew actions' sounds (`Demolition Men/Assets/Resources/Sounds/Actions/*.ogg`, played by
`Scripts/Demolition/Actions/View/ActionAudio.cs`). All come from Freesound, as the HQ ogg preview.
Licences are CC0 or CC BY 4.0 only: never NC, the game has ads. Every one is also in
`Resources/Credits.txt`. A file missing from the folder falls back to the synthesised sound; without
`Radio.ogg` the radio plays its synth tune.

Processing (ffmpeg): from *start*, leading silence below -50 dB removed, cut to *length*, mono 44.1 kHz,
faded out over *fade*, peak-normalised to -1 dBFS, Vorbis q5.

| File | Used for | Freesound sound | Author | Licence | Start | Length | Fade | Extra |
|---|---|---|---|---|---|---|---|---|
| Can | the burp: can opened | [soda can open.WAV](https://freesound.org/s/703669/) | sandroschroeder | CC0 | 0 | 0.38 | 0.06 | |
| Gulp | burp and coffee: gulps | [Gulp (2)](https://freesound.org/s/151233/) | OwlStorm | CC0 | 0 | 0.18 | 0.04 | |
| Burp | the burp | [belch1.wav](https://freesound.org/s/26762/) | santuccie | CC0 | 0 | 0.74 | 0.12 | |
| Whoosh | butt slam leap, breath in before the burp | [Swosh / Whoosh / Air Cut](https://freesound.org/s/60030/) | qubodup | CC0 | 0 | 0.44 | 0.08 | |
| Rip | butt slam: trousers tearing | [cloth_rip.mp3](https://freesound.org/s/507756/) | Johnny2810 | CC0 | 0 | 0.50 | 0.08 | |
| Thud | every heavy landing (with the synth rumble), radio set down | [thud.wav](https://freesound.org/s/215162/) | OtisJames | CC0 | 0 | 0.17 | 0.03 | |
| Whistle | the crane called: finger whistle | [Whistle, Finger, Short, A.wav](https://freesound.org/s/423285/) | InspectorJ | CC BY 4.0 | 0 | 0.75 | 0.25 | |
| Falling | the crane's pallet coming down | [Falling Bomb.wav](https://freesound.org/s/506313/) | Daleonfire | CC0 | 0 | 1.25 | 0.30 | atempo 2.0 |
| Plunger | controlled demolition: plunger pulled up | [Mechanical switch (latch) 02](https://freesound.org/s/815493/) | xkeril | CC0 | 0 | 0.35 | 0.10 | |
| Beep | the 3-2-1 | [beep.wav](https://freesound.org/s/391650/) | jeckkech | CC0 | 0 | 0.19 | 0.02 | |
| Pour | coffee poured | [Pouring Drink Sound Effect by Rea.mp3](https://freesound.org/s/382911/) | reasanka | CC0 | 0.35 | 0.75 | 0.20 | |
| Paper | the intern's blueprint | [Paper Rustle](https://freesound.org/s/353125/) | BenjaminNelan | CC0 | 0.2 | 0.75 | 0.15 | |
| Pop | the intern in and out | [Cartoon Pop (Clean)](https://freesound.org/s/245645/) | unfa | CC0 | 0 | 0.17 | 0.03 | |
| Velcro | the hi-vis vest on | [Velcro Pull Short](https://freesound.org/s/536228/) | Rudmer_Rotteveel | CC0 | 0 | 0.25 | 0.05 | |
| Helmet | the hard hat on | [plastic donk 4.wav](https://freesound.org/s/455193/) | deleted Freesound user | CC0 | 0 | 0.45 | 0.15 | |
| Boing | rubble bouncing off the vest | [Boing 1](https://freesound.org/s/540788/) | magnuswaker | CC0 | 0 | 0.50 | 0.15 | |
| Shovel | the shovel stuck in the ground | [Shovel - Sand/Gravel/Snow](https://freesound.org/s/560957/) | Bricklover | CC0 | 0.25 | 0.35 | 0.10 | |
| Static | the radio switched on | [radio noise 2.wav](https://freesound.org/s/30335/) | ERH | CC BY 4.0 | 0 | 0.70 | 0.30 | |
| Kick | the radio's beat | [Kick_Drum_Hard_#3.wav](https://freesound.org/s/190551/) | Renovatio871 | CC0 | 0 | 0.30 | 0.10 | |
| Radio | the radio's music (8 s = the radio's duration, 120 bpm = its 0.5 s beat; the loop's kick is on 0.0 s) | [Disco funk loops 001 remix 2 short loop with drums 120 bpm](https://freesound.org/s/569377/) | josefpres | CC0 | 0 | 8.00 | 0.60 | high-pass 150 Hz, low-pass 6 kHz (a site radio) |

Still synthesised: the slam's rumble, the buffs' power-up, the radio's tune (only without `Radio.ogg`).
