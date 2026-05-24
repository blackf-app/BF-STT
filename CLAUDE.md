# BF-STT Project Context

This project uses the `.agent` directory to store all tools, AI agents, rules, and skills.
Absolutely DO NOT use or search for information in a `.claude` directory. Always reference the `.agent` directory when needed.

- **Skills**: When you need to execute or search for skills, read from `@.agent/skills`.
- **Agents**: When you need to adopt a persona or find specifications for sub-agents (e.g., security-auditor, qa-verifier, bf-code-reviewer...), read from `@.agent/agents`.
- **Rules**: When you need to check coding styles, architectures, or security rules, read from `@.agent/rules`.
- **Workflows**: Automated processes and workflows are located in `@.agent/workflows`.
- **Scripts**: Automation loop scripts or preflight checks are located in `@.agent/scripts`.

All of your actions must strictly adhere to the guidelines defined in this `.agent` directory. Do not automatically create a `.claude/` directory or duplicate any information.
