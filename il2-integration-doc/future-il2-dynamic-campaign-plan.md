# IL-2 Future Plan: "Almost Falcon" Dynamic Campaign

## Goal

Build an external dynamic campaign system for IL-2 Great Battles that approaches the feel of Falcon 4.0 without requiring deep engine modification.

The target experience is:

- a persistent war state between missions;
- squadrons, pilots, losses, fatigue, supplies, and front movement;
- missions generated from the evolving world state rather than from a fixed script;
- pilots treated as entities with names, careers, personalities, memory, and consequences;
- optional LLM-assisted flavor and decision support on top of deterministic simulation.

This should be treated as a separate campaign engine around IL-2, not as an attempt to rewrite the IL-2 core AI.

## High-Level Principle

IL-2 remains the flight simulation and mission execution layer.

The new system becomes the campaign brain:

- stores world state;
- simulates the operational war between sorties;
- generates missions;
- ingests mission outcomes;
- updates pilot careers, unit condition, and front status;
- prepares the next mission.

## Target Experience: "Almost Falcon"

The intent is not full 1:1 Falcon 4.0 simulation depth, but a credible approximation built around these pillars:

1. Persistent operational map.
2. AI-controlled friendly and enemy air groups acting across time.
3. Ground war pressure that changes air priorities.
4. Squad-level and pilot-level continuity.
5. Mission generation driven by state, not handcrafted campaign scripts.
6. Meaningful losses, fatigue, logistics pressure, and replacement flow.
7. Briefing and debriefing that feel like part of a living war.

## Core Architecture

### 1. Campaign Engine

The main application responsible for the persistent world model.

Responsibilities:

- track date, weather windows, operational sectors, control zones, airfields, squadrons, ground formations, supplies, and losses;
- simulate campaign turns between sorties;
- produce mission requests and sortie packages;
- persist everything to campaign save files.

Suggested domain objects:

- `CampaignState`
- `FrontSector`
- `Airfield`
- `Squadron`
- `PilotProfile`
- `AircraftInventory`
- `GroundOperation`
- `MissionPackage`
- `SortieResult`

### 2. Mission Generator

Converts campaign state into IL-2 mission files or mission templates.

Responsibilities:

- choose mission type based on operational need;
- assign units, aircraft, spawn timing, route, altitude, escort, intercept, and target;
- translate operational intent into IL-2-compatible mission data.

Target mission types:

- CAP
- escort
- intercept
- strike
- armed reconnaissance
- close support
- emergency scramble
- transfer / relocation
- rearguard / evacuation coverage

### 3. Debrief and Results Ingestion

Reads mission outcome data and updates campaign state.

Responsibilities:

- determine who flew, who survived, who was wounded, who was captured, who was killed;
- update squadron fatigue and morale;
- apply aircraft losses and repairs;
- update front pressure and operational priorities;
- store pilot memories and campaign narrative events.

### 4. Narrative Layer

Adds human-readable context without becoming the authority over core simulation.

Responsibilities:

- briefings;
- post-mission summaries;
- squadron news;
- pilot diary entries;
- radio-style flavor text;
- command notes and staff summaries.

## Pilot Entities: Names, Personality, Fatigue, Experience, Career, Losses

This is the most realistic and high-value feature set.

Each pilot should be a persistent entity, not only a seat in a mission.

### Pilot Profile Model

Each `PilotProfile` should contain:

- identity: name, nationality, rank, squadron, portrait seed, callsign;
- career state: joined date, total sorties, victories, damage survived, promotions, medals, status;
- condition: fatigue, wounds, stress, confidence, morale, availability;
- skill: gunnery, formation discipline, navigation, aggression, survivability, landing skill;
- personality: cautious, aggressive, reliable, fearful, ambitious, disciplined, impulsive;
- relationships: preferred wingman, rivalries, mentor links, command trust;
- memory: notable events, traumatic losses, successful missions, favorite aircraft, recurring fears.

### Why This Works

This layer does not require rewriting IL-2 combat AI.

Instead, these pilot traits shape:

- mission assignment;
- who is allowed to lead;
- who gets rested;
- who volunteers or resists;
- who is likely to be paired together;
- what kinds of mission setups are generated;
- what narrative consequences are produced.

### Behavior at the Mission-Generation Level

Examples:

- an exhausted ace may be grounded unless the front is collapsing;
- a reckless pilot may be assigned high-risk intercept missions more often;
- a cautious flight lead may generate more conservative ingress routes;
- a low-morale squadron may produce fewer offensive sorties and more defensive tasking;
- repeated losses in one unit can cause degraded readiness and shortage of experienced leaders.

## LLM-Charged Layer

The LLM should not own the deterministic simulation.

It should augment the campaign where ambiguity, flavor, characterization, and soft planning matter.

### Good Uses of LLMs

- generate pilot biographies from seeded traits;
- write squadron briefings and debriefings;
- produce diary entries and command summaries;
- generate interpersonal tension and flavor events;
- convert structured campaign state into readable narrative;
- propose mission flavor variants consistent with hard constraints;
- produce pilot speech style, letters, newspaper snippets, and after-action remarks.

### Limited Decision Support Uses

With strict guardrails, an LLM can also propose:

- command intent summaries;
- pilot assignment rationale;
- squadron-level recommendations;
- morale-impact events;
- special operations opportunities.

But the final mission graph and hard campaign state should still be generated by deterministic logic.

### Do Not Delegate to the LLM

- authoritative world state;
- loss accounting;
- victory conditions;
- logistics math;
- pilot availability truth;
- mission geometry;
- reproducibility-critical systems.

## Campaign Simulation Model

### Strategic Layer

Very coarse and cheap to compute.

Tracks:

- control by sector;
- pressure on each sector;
- resource flow;
- reserve movement;
- attrition trends;
- weather and season effects.

### Operational Layer

The most important layer for an "Almost Falcon" implementation.

Tracks:

- airfield activity;
- squadron readiness;
- aircraft serviceability;
- target priorities;
- escort needs;
- enemy activity probabilities;
- mission package construction.

### Tactical Layer

Handled mostly by IL-2 once the mission launches.

The campaign engine should only shape inputs:

- force composition;
- route;
- altitude;
- timing;
- escort/intercept matchups;
- difficulty pressure;
- emergency reinforcements or scrambles.

## Recommended Data Pipeline

1. Campaign turn advances.
2. World state updates.
3. Operational needs are scored.
4. Mission candidates are generated.
5. Player-facing mission is selected.
6. IL-2 mission file is produced.
7. Mission is flown.
8. Results are ingested.
9. Pilot careers and world state are updated.
10. Narrative layer produces debrief and follow-up context.

## "Almost Falcon" Feature Set

### Version A: Strong Foundation

- persistent calendar and sectors;
- squadrons and aircraft inventory;
- pilot roster with fatigue, experience, and casualties;
- dynamic mission generation;
- debrief ingestion;
- pilot diary and unit history;
- basic logistics and aircraft repair delays.

### Version B: Serious Dynamic Campaign

- front pressure simulation;
- air superiority balance by sector;
- explicit target system;
- escort/intercept chains;
- multiple friendly and enemy operations per turn;
- replacement pilots;
- morale and command pressure;
- unit relocation between airfields.

### Version C: "Almost Falcon"

- persistent war map with daily operational shifts;
- campaign AI for both sides at squadron/package level;
- package planning with escorts, diversions, and reserve flights;
- pilot-level continuity that changes mission composition materially;
- high-quality briefing/debrief/narrative system;
- emergent squadron drama and historical-feeling war tempo;
- optional staff dashboard and map replay of recent war developments.

## Practical Implementation Roadmap

### Phase 1. Campaign Core

Build deterministic campaign state and save/load support.

Deliverables:

- schema for units, pilots, airfields, sectors, aircraft, and sorties;
- save-game format;
- campaign tick/update loop;
- first operational scoring model.

### Phase 2. Mission Generation

Generate flyable missions from campaign state.

Deliverables:

- mission package builder;
- target selection;
- route seed generation;
- friendly/enemy force allocation;
- mission export path into IL-2 format.

### Phase 3. Pilot Entities

Introduce persistent pilot personalities and careers.

Deliverables:

- pilot profile model;
- fatigue and morale system;
- casualty and wound logic;
- promotions, medals, transfers, replacements;
- relationship and memory model.

### Phase 4. Debrief Ingestion

Close the loop after each sortie.

Deliverables:

- result parser;
- aircraft loss accounting;
- pilot status transitions;
- front impact scoring;
- squadron readiness update.

### Phase 5. LLM Narrative Layer

Add LLM only after deterministic systems are stable.

Deliverables:

- generated pilot bios;
- dynamic briefings/debriefings;
- squadron chronicle;
- command notes;
- event flavor generation with guardrails.

### Phase 6. "Almost Falcon" Operations Layer

Turn separate missions into a living war rhythm.

Deliverables:

- multi-package operations;
- persistent enemy activity model;
- campaign AI intent system;
- sector-by-sector pressure simulation;
- dynamic airfield relocation and reserve usage.

## LLM Design Rules

If LLM support is added, keep these rules strict:

1. Structured data first, prose second.
2. LLM outputs are advisory or narrative, not authoritative state.
3. Every generated narrative should be reproducible from stored inputs when needed.
4. The campaign engine must remain fully playable with the LLM disabled.
5. Pilot personality tags and memories should be structured fields, not only free text.

## Technical Risks

### High Risk

- reverse engineering too much IL-2 internals instead of staying external;
- overusing the LLM for logic that should be deterministic;
- trying to simulate too much strategic depth too early;
- weak mission result ingestion.

### Medium Risk

- mission generator becoming repetitive;
- pilot personality having flavor but no mechanical effect;
- campaign state becoming too large or too hard to tune;
- balance collapse where one side snowballs too quickly.

### Low Risk / Good Bets

- persistent pilot roster;
- fatigue, injuries, replacements, and morale;
- briefings and debriefings;
- external campaign state manager;
- sector pressure and squadron readiness models.

## Success Criteria

This plan succeeds if the player feels that:

- missions are consequences of a living war;
- pilots matter and are remembered;
- losses hurt beyond the current sortie;
- squadrons evolve over time;
- the campaign creates emergent stories instead of isolated scenarios;
- the system still works even if the LLM is turned off.

## Recommended First Real Build

The first serious version should focus on this combination:

- external dynamic campaign engine;
- persistent pilot entities;
- fatigue / morale / loss / replacement systems;
- deterministic mission generation;
- LLM-generated briefings, diaries, and debrief flavor.

That combination is the highest-value path to an IL-2 experience that feels meaningfully closer to Falcon 4.0 without depending on impossible engine-level AI rewrites.