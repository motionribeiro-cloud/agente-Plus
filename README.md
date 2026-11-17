# Soma Agente 1.3.0

Experimental creative-software integrations connecting local Claude Code or Codex agents to application tools through MCP.

## Project status

Experimental source package prepared for publication. No public adoption metrics are available. Integrations listed below are present in source; compatibility and end-to-end behavior have not been verified during this review.

## Included integrations

- Adobe CEP panel with host adapters for After Effects, Photoshop, Illustrator and Premiere Pro.
- Figma development plugin with a local Python bridge.
- Blender add-on.
- Cinema 4D plugin.

## Architecture

The source includes local MCP servers and provider adapters for Claude Code and Codex. The Adobe implementation includes application scripting tools and code for media attachments, references and voice workflows. Availability depends on the application, provider and local dependencies. A subscription is not included.

## Source layout

- `source/Soma Agente/`: Adobe panel and installation scripts.
- `source/Figma/`: Figma plugin and bridge.
- `source/Blender/`: Blender add-on.
- `source/Cinema 4D/`: Cinema 4D integration.

The original installation instructions and README are preserved under `source/Soma Agente/`. The original README describes an older AE Agent version and does not establish validation of all 1.3.0 integrations.

## Intended direction

Develop a creative assistant informed by practical art direction and animation knowledge: composition, hierarchy, timing, easing and editable production workflows. This is the development vision, not a claim that every capability is already implemented.

## Authorship and license status

Igor Ribeiro confirmed authorship of both Soma Agente and the AE Agent 0.8.1 base referenced in the bundled documentation. The original documentation is preserved for provenance.

No distribution license has been selected for this package yet. Public source availability alone does not make a project open source. Reuse permissions will be defined by the author in a future LICENSE file.
