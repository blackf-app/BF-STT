# BF-STT Project Context

This project uses the `.agents` directory to store all tools, AI agents, rules, and skills.
Absolutely DO NOT use or search for information in a `.claude` directory. Always reference the `.agents` directory when needed.

- **Skills**: When you need to execute or search for skills, read from `@.agents/skills`.
- **Agents**: When you need to adopt a persona or find specifications for sub-agents (e.g., security-auditor, qa-verifier, bf-code-reviewer...), read from `@.agents/agents`.
- **Rules**: When you need to check coding styles, architectures, or security rules, read from `@.agents/rules`.
- **Workflows**: Automated processes and workflows are located in `@.agents/workflows`.
- **Scripts**: Automation loop scripts or preflight checks are located in `@.agents/scripts`.

All of your actions must strictly adhere to the guidelines defined in this `.agents` directory. Do not automatically create a `.claude/` directory or duplicate any information.
