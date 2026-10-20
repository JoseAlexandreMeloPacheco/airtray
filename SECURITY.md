# Segurança e privacidade / Security and privacy

## Português

AirTray envia comandos reais e autentica diretamente nos servidores do serviço. Use somente sua própria conta e aparelhos que você tem autorização para controlar. A senha fica no Gerenciador de Credenciais do Windows, sob seu usuário; sessões e chaves recebidas ficam em memória. O repositório e os pacotes não contêm a conta do desenvolvedor ou dados do aparelho dele. Não há telemetria própria do projeto.

O cliente usa TLS com validação padrão, sem redirecionamentos automáticos, limites de tamanho e prazos de resposta. Respostas inválidas não são tratadas como confirmação de controle. A integração contém constantes do aplicativo original; elas não são suas credenciais pessoais, e a ausência de senha pessoal não equivale a autorização de redistribuição. Veja THIRD_PARTY.md.

Não publique senhas, cookies, chaves, identificadores pessoais de aparelhos, sessões ou respostas brutas da nuvem em issues. Informe apenas versão, passos e código de erro. APK, ferramentas de análise, fontes decompiladas e arquivos locais permanecem fora da lista pública. Prepare-GitHub verifica caminhos no índice Git, mas não é um scanner universal de segredos.

O botão GitHub abre o navegador externo mediante ação do usuário. Para esquecer uma conta, use o menu de contexto de Minha conta. Não há canal privado de segurança configurado neste repositório; não publique detalhes sensíveis de falhas de autenticação.

## English

AirTray sends real commands and authenticates directly with the service's servers. Use only your own account and appliances you are authorized to control. Passwords are stored under your Windows user in Credential Manager; returned sessions and keys remain in memory. Neither the repository nor release packages include the developer's account or appliance data. The project has no telemetry of its own.

The client uses standard TLS validation, no automatic redirects, bounded response sizes and deadlines. Invalid responses are not treated as successful device control. It includes application compatibility constants; they are distinct from personal credentials, and absence of a personal password does not establish redistribution authorization. See THIRD_PARTY.md.

Do not publish passwords, cookies, keys, personal device identifiers, sessions or raw cloud responses in issues. Provide only the version, steps and error code. APKs, analysis tools, decompiled sources and local files are excluded from the public allowlist. Prepare-GitHub checks paths in the actual index but is not a universal secret scanner.

The GitHub button opens the external browser only at the user's request. To forget an account, use the Minha conta context menu. No private security reporting channel is configured; do not post sensitive authentication vulnerability details publicly.
