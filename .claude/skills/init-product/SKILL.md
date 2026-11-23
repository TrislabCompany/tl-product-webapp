---
name: init-product
description: Turn a new copy of this template into a product — choose the framework modules and optional features the product keeps, run scripts/init-product.mjs to remove the rest and the template-only files, fix the README and docs, and open the first pull request. Only in a repository that still has template-manifest.yaml. Use when the user asks to initialise, set up or start a product from the webapp template, or to remove a module the product does not need.
disable-model-invocation: true
metadata:
  owner: template-webapp
  ownership: template-only
---

# init-product

A pointer, so that Claude Code lists this skill: it finds skills only in `.claude/skills/`. Read [.agents/skills/init-product/SKILL.md](../../../.agents/skills/init-product/SKILL.md) and follow it exactly. That file is the skill; edit it, not this one. `pnpm --dir web test` checks that the front matter of both is the same.
