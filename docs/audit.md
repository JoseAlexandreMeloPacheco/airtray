# Auditoria / Audit — 0.3.0

PT: Escopo: cliente funcional experimental. Testes offline validam estados válidos em objeto e JSON serializado, limites de temperatura, energia inválida, índices inválidos, propriedades duplicadas, respostas malformadas e rejeição de seleção fora do catálogo da conta. Testes WPF validam imagens de estado, recolhimento, fechamento e reabertura. Não usam a conta do usuário nem enviam comandos físicos.

EN: Scope: experimental functional client. Offline tests cover valid object/string-encoded states, temperature bounds, invalid power/index values, duplicate properties, malformed responses and rejection of devices outside the account catalog. WPF tests cover state artwork, hiding, closing and reopening. They do not access a user account or send physical commands.

## Correções / Fixes

- Seleção explícita com vários aparelhos; nunca usar identificador pessoal fixo / Explicit multiple-device selection; no hardcoded personal device identifier.
- Validação de respostas antes de exibir estado confirmado / Validate responses before displaying confirmed state.
- Prazo cobre cabeçalhos e corpo; HTTP inválido rejeitado / Deadline covers headers and body; reject unsuccessful HTTP.
- Login cancelado antes da autenticação concluir não salva nova senha / Canceling login before authentication completes does not save a new password.
- Senha salva pode ser reutilizada após falha posterior à autenticação / Saved password can be reused after a post-authentication failure.
- Fechar recolhe para a bandeja; saída exige confirmação / Closing hides to tray; exiting requires confirmation.
- Conta salva pode ser removida pelo próprio usuário / Users can remove their own saved account.

## Integração real / Real integration

PT: Login, descoberta, leitura e mudança/restauração de temperatura foram validados com um aparelho e uma conta autorizados. Não houve teste físico de cada modo, várias contas, vários modelos ou da seleção com múltiplos aparelhos reais. A revisão não garante ausência de bugs, compatibilidade universal ou autorização jurídica de distribuição. Veja THIRD_PARTY.md.

EN: Login, discovery, reads and temperature change/restoration were validated with one authorized account and device. Every physical mode, multiple accounts, multiple models and a real multi-device selection scenario have not been tested. This review does not guarantee freedom from bugs, universal compatibility or legal distribution authorization. See THIRD_PARTY.md.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build.ps1 -Verify
```
