# Pull Requests históricos fuera de la convención semántica (R-06)

Convención actual (validada por `.github/workflows/pr-title.yml`):

```text
^(feat|fix|docs|test|refactor|ci|build|chore|perf|style)(\([a-z0-9._/-]+\))?!?: .+
```

Esta lista identifica títulos que **no** cumplen esa regla. **No se renombraron** automáticamente: hace falta autorización explícita del equipo antes de editar títulos en GitHub (los mergeados se pueden renombrar sin reescribir commits).

| PR | Título actual | Título propuesto | Estado |
|---|---|---|---|
| #57 | Feature/modo practica frontend | feat(frontend): implementar modo playground (práctica) | Pendiente de revisión |
| #56 | Feature/login unico shell | feat(auth): unificar login con la sesión del Shell | Pendiente de revisión |
| #54 | Implementar backend del modo practica | feat(api): implementar backend del modo playground | Pendiente de revisión |
| #50 | Limpieza de referencias Auth0 y actualizacion de configuracion | refactor(auth): limpiar referencias a Auth0 y actualizar configuración | Pendiente de revisión |
| #47 | Test/frontend cobertura 85 | test(frontend): alcanzar cobertura del 85 % | Pendiente de revisión |
| #44 | Quitar Auth0 del backend de Trivia | refactor(api): quitar Auth0 del backend de Trivia | Pendiente de revisión |
| #42 | Refactor(data): llaves Guid, una migración por tabla y script de preguntas (#27, #28) | refactor(data): llaves Guid, una migración por tabla y script de preguntas (#27, #28) | Pendiente de revisión |
| #22 | Chore/cliente m2m trivia | chore(matchmaking): configurar cliente M2M de Trivia | Pendiente de revisión |
| #17 | Feat/hub auth matchmaking | feat(api): integrar hub, autenticación y Matchmaking | Pendiente de revisión |
| #15 | Docs/integracion shell auth0 | docs: documentar integración Shell y Auth0 | Pendiente de revisión |
| #14 | Feat/completar motor partida | feat(domain): completar motor de partida | Pendiente de revisión |

Total identificados: **11**. El resto de PRs del repositorio ya usan prefijo semántico en minúsculas con `tipo:` o `tipo(ámbito):`.
