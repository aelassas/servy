#define MyAppName "Servy"
#ifndef MyAppVersion
  #define MyAppVersion "1.0"  ; default if not provided
#endif
#define MyAppPublisher "Akram El Assas"
#define MyAppURL "https://servy-win.github.io/"
#define DocsURL "https://github.com/aelassas/servy/wiki"
#define MyAppExeName "Servy.exe"

#define ManagerAppName "Servy Manager"
#define ManagerAppExeName "Servy.Manager.exe"

#define CliExeName "servy-cli.exe"

#ifndef Arch
  #define Arch "x64"
#endif

#ifndef BuildConfiguration
  #define BuildConfiguration "Release"
#endif

#ifndef Tfm
  #define Tfm "net10.0-windows"
#endif

#define AppIdGuidX64   "8343B121-BE1C-463F-AA5B-FD237DD2F8D0"
#define AppIdGuidArm64 "8343B121-BE1C-463F-AA5B-FD237DD2F8D1"

#if Arch == "arm64"
  #define AppIdGuid    AppIdGuidArm64
  #define ArchAllowed  "arm64"
  #define Runtime      "win-arm64"
#else
  #define AppIdGuid    AppIdGuidX64
  #define ArchAllowed  "x64compatible"
  #define Runtime      "win-x64"
#endif

[Setup]
PrivilegesRequired=admin
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{{#AppIdGuid}}
SetupMutex=ServySetupMutex
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DisableDirPage=no
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE.txt
OutputDir=.
OutputBaseFilename=servy-{#MyAppVersion}-{#Arch}-installer
SetupIconFile=..\src\Servy\servy.ico

Compression=lzma
LZMAAlgorithm=1
; LZMADictionarySize=65536
; LZMADictionarySize=98304
; LZMADictionarySize=131072
LZMADictionarySize=196608
LZMANumFastBytes=273
LZMAUseSeparateProcess=yes
SolidCompression=yes

; Allow setup to start on all 64-bit platforms so InitializeSetup can show an explicit error instead of Inno's generic Windows version message (#7481)
ArchitecturesAllowed=x64compatible arm64
ArchitecturesInstallIn64BitMode={#ArchAllowed}
WizardStyle=modern dynamic
ShowLanguageDialog=yes

UsePreviousTasks=no
UsePreviousSetupType=no
AlwaysRestart=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
english.SetupAppRunningError=Setup has detected that %1 is currently running.%n%nPlease close all instances of it now, then click OK to continue, or Cancel to exit.
chinesesimplified.SetupAppRunningError=安装程序检测到 %1 正在运行。%n%n请立即关闭它的所有实例，然后单击“确定”继续，或单击“取消”退出。
french.SetupAppRunningError=Le programme d'installation a détecté que %1 est en cours d'exécution.%n%nFermez maintenant toutes ses instances, puis cliquez sur OK pour continuer, ou sur Annuler pour quitter.
german.SetupAppRunningError=Das Setup hat festgestellt, dass %1 gerade ausgeführt wird.%n%nSchließen Sie jetzt alle Instanzen, und klicken Sie dann auf OK, um fortzufahren, oder auf Abbrechen, um das Setup zu beenden.
spanish.SetupAppRunningError=El programa de instalación detectó que %1 se está ejecutando.%n%nCierre ahora todas sus instancias y, a continuación, haga clic en Aceptar para continuar o en Cancelar para salir.
japanese.SetupAppRunningError=セットアップは %1 が実行中であることを検出しました。%n%n今すぐすべてのインスタンスを閉じてから、[OK] をクリックして続行するか、[キャンセル] をクリックして終了してください。
italian.SetupAppRunningError=Il programma di installazione ha rilevato che %1 è in esecuzione.%n%nChiudere ora tutte le istanze, quindi fare clic su OK per continuare oppure su Annulla per uscire.
portuguese.SetupAppRunningError=O programa de configuração detetou que %1 está em execução.%n%nFeche agora todas as instâncias e, em seguida, clique em OK para continuar ou em Cancelar para sair.
brazilianportuguese.SetupAppRunningError=O programa de instalação detectou que %1 está em execução no momento.%n%nFeche todas as instâncias agora e clique em OK para continuar ou em Cancelar para sair.

[CustomMessages]
; Types
english.FullInstallation=Full installation
chinesesimplified.FullInstallation=完整安装
french.FullInstallation=Installation complète
german.FullInstallation=Vollständige Installation
spanish.FullInstallation=Instalación completa
japanese.FullInstallation=完全インストール
italian.FullInstallation=Installazione completa
portuguese.FullInstallation=Instalação completa
brazilianportuguese.FullInstallation=Instalação completa
english.CustomInstallation=Custom installation
chinesesimplified.CustomInstallation=自定义安装
french.CustomInstallation=Installation personnalisée
german.CustomInstallation=Benutzerdefinierte Installation
spanish.CustomInstallation=Instalación personalizada
japanese.CustomInstallation=カスタム インストール
italian.CustomInstallation=Installazione personalizzata
portuguese.CustomInstallation=Instalação personalizada
brazilianportuguese.CustomInstallation=Instalação personalizada

; Components
english.InstallDesktopApp=Install Desktop App (%1)
chinesesimplified.InstallDesktopApp=安装桌面应用程序 (%1)
french.InstallDesktopApp=Installer l'application de bureau (%1)
german.InstallDesktopApp=Desktop-App installieren (%1)
spanish.InstallDesktopApp=Instalar la aplicación de escritorio (%1)
japanese.InstallDesktopApp=デスクトップ アプリをインストール (%1)
italian.InstallDesktopApp=Installa l'app desktop (%1)
portuguese.InstallDesktopApp=Instalar a aplicação de ambiente de trabalho (%1)
brazilianportuguese.InstallDesktopApp=Instalar o aplicativo da área de trabalho (%1)
english.InstallCli=Install CLI (%1)
chinesesimplified.InstallCli=安装命令行工具 (%1)
french.InstallCli=Installer l'interface de ligne de commande (%1)
german.InstallCli=Befehlszeilentool installieren (%1)
spanish.InstallCli=Instalar la herramienta de línea de comandos (%1)
japanese.InstallCli=コマンド ライン ツールをインストール (%1)
italian.InstallCli=Installa lo strumento da riga di comando (%1)
portuguese.InstallCli=Instalar a ferramenta da linha de comandos (%1)
brazilianportuguese.InstallCli=Instalar a ferramenta de linha de comando (%1)
english.InstallManagerApp=Install Manager App (%1)
chinesesimplified.InstallManagerApp=安装管理器应用程序 (%1)
french.InstallManagerApp=Installer l'application Manager (%1)
german.InstallManagerApp=Manager-App installieren (%1)
spanish.InstallManagerApp=Instalar la aplicación Manager (%1)
japanese.InstallManagerApp=Manager アプリをインストール (%1)
italian.InstallManagerApp=Installa l'app Manager (%1)
portuguese.InstallManagerApp=Instalar a aplicação Manager (%1)
brazilianportuguese.InstallManagerApp=Instalar o aplicativo Manager (%1)

; Tasks & Groups
english.AdditionalOptions=Additional Options
chinesesimplified.AdditionalOptions=附加选项
french.AdditionalOptions=Options supplémentaires
german.AdditionalOptions=Zusätzliche Optionen
spanish.AdditionalOptions=Opciones adicionales
japanese.AdditionalOptions=追加オプション
italian.AdditionalOptions=Opzioni aggiuntive
portuguese.AdditionalOptions=Opções adicionais
brazilianportuguese.AdditionalOptions=Opções adicionais
english.AddServyToPath=Add Servy to PATH
chinesesimplified.AddServyToPath=将 Servy 添加到环境变量 PATH
french.AddServyToPath=Ajouter Servy à la variable d'environnement PATH
german.AddServyToPath=Servy zur Umgebungsvariablen PATH hinzufügen
spanish.AddServyToPath=Agregar Servy a la variable de entorno PATH
japanese.AddServyToPath=Servy を環境変数 PATH に追加
italian.AddServyToPath=Aggiungi Servy alla variabile di ambiente PATH
portuguese.AddServyToPath=Adicionar o Servy à variável de ambiente PATH
brazilianportuguese.AddServyToPath=Adicionar o Servy à variável de ambiente PATH

; Icons & Run
english.UninstallShortcut=Uninstall
chinesesimplified.UninstallShortcut=卸载
french.UninstallShortcut=Désinstaller
german.UninstallShortcut=Deinstallieren
spanish.UninstallShortcut=Desinstalar
japanese.UninstallShortcut=アンインストール
italian.UninstallShortcut=Disinstalla
portuguese.UninstallShortcut=Desinstalar
brazilianportuguese.UninstallShortcut=Desinstalar
english.OpenDocumentation=Open Documentation
chinesesimplified.OpenDocumentation=打开在线文档
french.OpenDocumentation=Ouvrir la documentation en ligne
german.OpenDocumentation=Onlinedokumentation öffnen
spanish.OpenDocumentation=Abrir la documentación en línea
japanese.OpenDocumentation=オンライン ドキュメントを開く
italian.OpenDocumentation=Apri la documentazione online
portuguese.OpenDocumentation=Abrir a documentação online
brazilianportuguese.OpenDocumentation=Abrir a documentação online

; [Code] Wizard & Error Messages
english.SelectAtLeastOneComponent=You must select at least one component to continue.
chinesesimplified.SelectAtLeastOneComponent=您必须至少选择一个组件才能继续。
french.SelectAtLeastOneComponent=Vous devez sélectionner au moins un composant pour continuer.
german.SelectAtLeastOneComponent=Sie müssen mindestens eine Komponente auswählen, um fortzufahren.
spanish.SelectAtLeastOneComponent=Debe seleccionar al menos un componente para continuar.
japanese.SelectAtLeastOneComponent=続行するには、少なくとも 1 つのコンポーネントを選択する必要があります。
italian.SelectAtLeastOneComponent=Per continuare è necessario selezionare almeno un componente.
portuguese.SelectAtLeastOneComponent=Tem de selecionar pelo menos um componente para continuar.
brazilianportuguese.SelectAtLeastOneComponent=Você deve selecionar pelo menos um componente para continuar.

english.Arm64ArchMismatchError=This installer is built for ARM64 systems. Please download and run the x64 installer (servy-%1-x64-installer.exe).
chinesesimplified.Arm64ArchMismatchError=此安装程序专为 ARM64 系统构建。请下载并运行 x64 安装程序 (servy-%1-x64-installer.exe)。
french.Arm64ArchMismatchError=Ce programme d'installation est conçu pour les systèmes ARM64. Téléchargez et exécutez le programme d'installation x64 (servy-%1-x64-installer.exe).
german.Arm64ArchMismatchError=Dieses Installationsprogramm ist für ARM64-Systeme erstellt. Laden Sie das x64-Installationsprogramm (servy-%1-x64-installer.exe) herunter, und führen Sie es aus.
spanish.Arm64ArchMismatchError=Este instalador está diseñado para sistemas ARM64. Descargue y ejecute el instalador x64 (servy-%1-x64-installer.exe).
japanese.Arm64ArchMismatchError=このインストーラーは ARM64 システム用にビルドされています。x64 インストーラー (servy-%1-x64-installer.exe) をダウンロードして実行してください。
italian.Arm64ArchMismatchError=Questo programma di installazione è progettato per i sistemi ARM64. Scaricare ed eseguire il programma di installazione x64 (servy-%1-x64-installer.exe).
portuguese.Arm64ArchMismatchError=Este instalador foi concebido para sistemas ARM64. Transfira e execute o instalador x64 (servy-%1-x64-installer.exe).
brazilianportuguese.Arm64ArchMismatchError=Este instalador foi criado para sistemas ARM64. Baixe e execute o instalador x64 (servy-%1-x64-installer.exe).

english.X64OnArm64Warning=You are running the x64 installer on an ARM64 system. An ARM64-native version (servy-%1-arm64-installer.exe) is available for optimal performance.%n%nDo you want to continue with the x64 installation anyway?
chinesesimplified.X64OnArm64Warning=您正在 ARM64 系统上运行 x64 安装程序。我们提供了原生 ARM64 版本 (servy-%1-arm64-installer.exe) 以获得最佳性能。%n%n是否仍要继续运行 x64 安装？
french.X64OnArm64Warning=Vous exécutez le programme d'installation x64 sur un système ARM64. Une version ARM64 native (servy-%1-arm64-installer.exe) est disponible pour des performances optimales.%n%nVoulez-vous quand même continuer l'installation x64 ?
german.X64OnArm64Warning=Sie führen das x64-Installationsprogramm auf einem ARM64-System aus. Für optimale Leistung ist eine native ARM64-Version (servy-%1-arm64-installer.exe) verfügbar.%n%nMöchten Sie die x64-Installation trotzdem fortsetzen?
spanish.X64OnArm64Warning=Está ejecutando el instalador x64 en un sistema ARM64. Hay disponible una versión nativa para ARM64 (servy-%1-arm64-installer.exe) que ofrece un rendimiento óptimo.%n%n¿Desea continuar de todos modos con la instalación x64?
japanese.X64OnArm64Warning=ARM64 システムで x64 インストーラーを実行しています。最適なパフォーマンスを得られる ARM64 ネイティブ版 (servy-%1-arm64-installer.exe) が用意されています。%n%nこのまま x64 版のインストールを続行しますか?
italian.X64OnArm64Warning=Si sta eseguendo il programma di installazione x64 su un sistema ARM64. Per prestazioni ottimali è disponibile una versione nativa ARM64 (servy-%1-arm64-installer.exe).%n%nContinuare comunque con l'installazione x64?
portuguese.X64OnArm64Warning=Está a executar o instalador x64 num sistema ARM64. Está disponível uma versão nativa ARM64 (servy-%1-arm64-installer.exe) para um desempenho ideal.%n%nPretende continuar mesmo assim com a instalação x64?
brazilianportuguese.X64OnArm64Warning=Você está executando o instalador x64 em um sistema ARM64. Uma versão nativa ARM64 (servy-%1-arm64-installer.exe) está disponível para obter o desempenho ideal.%n%nDeseja continuar com a instalação x64 mesmo assim?

english.UpgradePrompt=An older version of Servy is already installed. Would you like to upgrade to this newer version?
chinesesimplified.UpgradePrompt=检测到已安装旧版本的 Servy。是否要升级到此新版本？
french.UpgradePrompt=Une version antérieure de Servy est déjà installée. Voulez-vous la mettre à niveau vers cette nouvelle version ?
german.UpgradePrompt=Eine ältere Version von Servy ist bereits installiert. Möchten Sie auf diese neuere Version aktualisieren?
spanish.UpgradePrompt=Ya hay instalada una versión anterior de Servy. ¿Desea actualizarla a esta versión más reciente?
japanese.UpgradePrompt=以前のバージョンの Servy が既にインストールされています。この新しいバージョンにアップグレードしますか?
italian.UpgradePrompt=Una versione precedente di Servy è già installata. Aggiornarla a questa versione più recente?
portuguese.UpgradePrompt=Já está instalada uma versão anterior do Servy. Pretende atualizar para esta versão mais recente?
brazilianportuguese.UpgradePrompt=Uma versão anterior do Servy já está instalada. Deseja atualizar para esta versão mais recente?

english.DowngradePrompt=A newer version of Servy is already installed. Are you sure you want to downgrade to this older version?
chinesesimplified.DowngradePrompt=检测到已安装更新版本的 Servy。确定要降级到此旧版本吗？
french.DowngradePrompt=Une version plus récente de Servy est déjà installée. Voulez-vous vraiment revenir à cette version antérieure ?
german.DowngradePrompt=Eine neuere Version von Servy ist bereits installiert. Möchten Sie wirklich auf diese ältere Version zurückstufen?
spanish.DowngradePrompt=Ya hay instalada una versión más reciente de Servy. ¿Está seguro de que desea cambiar a esta versión anterior?
japanese.DowngradePrompt=新しいバージョンの Servy が既にインストールされています。この古いバージョンにダウングレードしてもよろしいですか?
italian.DowngradePrompt=Una versione più recente di Servy è già installata. Tornare davvero a questa versione precedente?
portuguese.DowngradePrompt=Já está instalada uma versão mais recente do Servy. Tem a certeza de que pretende reverter para esta versão anterior?
brazilianportuguese.DowngradePrompt=Uma versão mais recente do Servy já está instalada. Tem certeza de que deseja reverter para esta versão anterior?

english.ReinstallPrompt=The same version of Servy is already installed. Would you like to reinstall it?
chinesesimplified.ReinstallPrompt=检测到已安装相同版本的 Servy。是否要重新安装？
french.ReinstallPrompt=La même version de Servy est déjà installée. Voulez-vous la réinstaller ?
german.ReinstallPrompt=Dieselbe Version von Servy ist bereits installiert. Möchten Sie sie erneut installieren?
spanish.ReinstallPrompt=Ya está instalada la misma versión de Servy. ¿Desea volver a instalarla?
japanese.ReinstallPrompt=同じバージョンの Servy が既にインストールされています。再インストールしますか?
italian.ReinstallPrompt=La stessa versione di Servy è già installata. Reinstallarla?
portuguese.ReinstallPrompt=Já está instalada a mesma versão do Servy. Pretende reinstalá-la?
brazilianportuguese.ReinstallPrompt=A mesma versão do Servy já está instalada. Deseja reinstalá-la?

english.CreateDataDirFailed=Failed to create the service data directory "%1".
chinesesimplified.CreateDataDirFailed=无法创建服务数据目录“%1”。
french.CreateDataDirFailed=Impossible de créer le répertoire de données du service « %1 ».
german.CreateDataDirFailed=Das Datenverzeichnis des Dienstes „%1“ konnte nicht erstellt werden.
spanish.CreateDataDirFailed=No se pudo crear el directorio de datos del servicio "%1".
japanese.CreateDataDirFailed=サービス データ ディレクトリ "%1" を作成できませんでした。
italian.CreateDataDirFailed=Impossibile creare la directory dei dati del servizio "%1".
portuguese.CreateDataDirFailed=Não foi possível criar a pasta de dados do serviço "%1".
brazilianportuguese.CreateDataDirFailed=Não foi possível criar a pasta de dados do serviço "%1".

english.DataDirReparsePointWarning=The service data directory "%1" is a symbolic link or junction, so setup did not secure it. Replace it with a real directory.
chinesesimplified.DataDirReparsePointWarning=服务数据目录“%1”是符号链接或联接点，因此安装程序未对其进行权限保护。请将其替换为真实目录。
french.DataDirReparsePointWarning=Le répertoire de données du service « %1 » est un lien symbolique ou une jonction ; le programme d'installation ne l'a donc pas sécurisé. Remplacez-le par un répertoire réel.
german.DataDirReparsePointWarning=Das Datenverzeichnis des Dienstes „%1“ ist eine symbolische Verknüpfung oder ein Verbindungspunkt, daher hat das Setup es nicht geschützt. Ersetzen Sie es durch ein echtes Verzeichnis.
spanish.DataDirReparsePointWarning=El directorio de datos del servicio "%1" es un vínculo simbólico o una unión, por lo que el programa de instalación no lo protegió. Reemplácelo por un directorio real.
japanese.DataDirReparsePointWarning=サービス データ ディレクトリ "%1" はシンボリック リンクまたはジャンクションであるため、セットアップはアクセス許可を保護しませんでした。実際のディレクトリに置き換えてください。
italian.DataDirReparsePointWarning=La directory dei dati del servizio "%1" è un collegamento simbolico o una giunzione, pertanto il programma di installazione non l'ha protetta. Sostituirla con una directory reale.
portuguese.DataDirReparsePointWarning=A pasta de dados do serviço "%1" é uma ligação simbólica ou uma junção, pelo que o programa de configuração não a protegeu. Substitua-a por uma pasta real.
brazilianportuguese.DataDirReparsePointWarning=A pasta de dados do serviço "%1" é um link simbólico ou uma junção; portanto, o programa de instalação não a protegeu. Substitua-a por uma pasta real.

english.DataDirOwnershipFailed=Failed to give the ownership of the service data directory "%1" to Administrators.
chinesesimplified.DataDirOwnershipFailed=无法将服务数据目录“%1”的所有权授予 Administrators 组。
french.DataDirOwnershipFailed=Impossible d'attribuer la propriété du répertoire de données du service « %1 » au groupe Administrateurs.
german.DataDirOwnershipFailed=Der Besitz des Datenverzeichnisses des Dienstes „%1“ konnte nicht an die Gruppe „Administratoren“ übertragen werden.
spanish.DataDirOwnershipFailed=No se pudo asignar la propiedad del directorio de datos del servicio "%1" al grupo Administradores.
japanese.DataDirOwnershipFailed=サービス データ ディレクトリ "%1" の所有権を Administrators グループに付与できませんでした。
italian.DataDirOwnershipFailed=Impossibile assegnare la proprietà della directory dei dati del servizio "%1" al gruppo Administrators.
portuguese.DataDirOwnershipFailed=Não foi possível atribuir a propriedade da pasta de dados do serviço "%1" ao grupo Administradores.
brazilianportuguese.DataDirOwnershipFailed=Não foi possível atribuir a propriedade da pasta de dados do serviço "%1" ao grupo Administradores.

english.DataDirPermissionsFailed=Failed to secure the permissions of the service data directory "%1".
chinesesimplified.DataDirPermissionsFailed=无法保护服务数据目录“%1”的访问权限。
french.DataDirPermissionsFailed=Impossible de sécuriser les autorisations du répertoire de données du service « %1 ».
german.DataDirPermissionsFailed=Die Berechtigungen des Datenverzeichnisses des Dienstes „%1“ konnten nicht geschützt werden.
spanish.DataDirPermissionsFailed=No se pudieron proteger los permisos del directorio de datos del servicio "%1".
japanese.DataDirPermissionsFailed=サービス データ ディレクトリ "%1" のアクセス許可を保護できませんでした。
italian.DataDirPermissionsFailed=Impossibile proteggere le autorizzazioni della directory dei dati del servizio "%1".
portuguese.DataDirPermissionsFailed=Não foi possível proteger as permissões da pasta de dados do serviço "%1".
brazilianportuguese.DataDirPermissionsFailed=Não foi possível proteger as permissões da pasta de dados do serviço "%1".

english.UninstallPreviousFailed=Failed to uninstall the previous version.
chinesesimplified.UninstallPreviousFailed=卸载旧版本失败。
french.UninstallPreviousFailed=Échec de la désinstallation de la version précédente.
german.UninstallPreviousFailed=Die vorherige Version konnte nicht deinstalliert werden.
spanish.UninstallPreviousFailed=No se pudo desinstalar la versión anterior.
japanese.UninstallPreviousFailed=以前のバージョンをアンインストールできませんでした。
italian.UninstallPreviousFailed=Impossibile disinstallare la versione precedente.
portuguese.UninstallPreviousFailed=Não foi possível desinstalar a versão anterior.
brazilianportuguese.UninstallPreviousFailed=Não foi possível desinstalar a versão anterior.

english.UpdateSystemPathFailed=Failed to update system PATH environment variable.
chinesesimplified.UpdateSystemPathFailed=更新系统 PATH 环境变量失败。
french.UpdateSystemPathFailed=Échec de la mise à jour de la variable d'environnement PATH du système.
german.UpdateSystemPathFailed=Die Systemumgebungsvariable PATH konnte nicht aktualisiert werden.
spanish.UpdateSystemPathFailed=No se pudo actualizar la variable de entorno PATH del sistema.
japanese.UpdateSystemPathFailed=システム環境変数 PATH を更新できませんでした。
italian.UpdateSystemPathFailed=Impossibile aggiornare la variabile di ambiente PATH di sistema.
portuguese.UpdateSystemPathFailed=Não foi possível atualizar a variável de ambiente PATH do sistema.
brazilianportuguese.UpdateSystemPathFailed=Não foi possível atualizar a variável de ambiente PATH do sistema.

[Types]
Name: "full"; Description: "{cm:FullInstallation}"
Name: "custom"; Description: "{cm:CustomInstallation}"; Flags: iscustom

[Components]
Name: "install_main_app"; Description: "{cm:InstallDesktopApp,{#MyAppExeName}}"; Types: full
Name: "install_cli"; Description: "{cm:InstallCli,{#CliExeName}}"; Types: full custom
Name: "install_manager"; Description: "{cm:InstallManagerApp,{#ManagerAppExeName}}"; Types: full custom

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: checkablealone
Name: "addpath"; Description: "{cm:AddServyToPath}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: checkablealone; Components: install_cli

[Files]
; Main app EXE
Source: "..\src\Servy\bin\{#BuildConfiguration}\{#Tfm}\{#Runtime}\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: install_main_app

; appsettings.desktop.json (only copy if not present, and never uninstall)
; Source: "..\src\Servy\appsettings.desktop.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_main_app

; CLI
Source: "..\src\Servy.CLI\bin\{#BuildConfiguration}\{#Tfm}\{#Runtime}\publish\Servy.CLI.exe"; DestDir: "{app}"; DestName: "{#CliExeName}"; Flags: ignoreversion; Components: install_cli

; appsettings.cli.json (only copy if not present, and never uninstall)
; Source: "..\src\Servy.CLI\appsettings.cli.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_cli

; PowerShell Module and Dump/Restore Scripts
Source: "..\src\Servy.CLI\Servy.psm1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\Servy.psd1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\servy-module-examples.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\Servy-Dump.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli
Source: "..\src\Servy.CLI\Servy-Restore.ps1"; DestDir: "{app}"; Flags: ignoreversion; Components: install_cli

; Manager
Source: "..\src\Servy.Manager\bin\{#BuildConfiguration}\{#Tfm}\{#Runtime}\publish\{#ManagerAppExeName}"; DestDir: "{app}"; Flags: ignoreversion; Components: install_manager

; appsettings.manager.json (only copy if not present, and never uninstall)
; Source: "..\src\Servy.Manager\appsettings.manager.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall; Components: install_manager

; taskschd
; 1. Copy everything EXCEPT the config, credentials, transient state files, and test/temp scripts
Source: ".\taskschd\*"; DestDir: "{app}\taskschd"; Excludes: "smtp-config.xml, smtp-cred.xml, *.dat, *.log, temp.ps1, *.test.ps1"; Flags: ignoreversion

; 2. Preserve the config file on upgrades
Source: ".\taskschd\smtp-config.xml"; DestDir: "{app}\taskschd"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall

; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Dirs]
; Name: "{commonappdata}\Servy"; Permissions: networkservice-modify service-modify
Name: "{commonappdata}\Servy"

[Icons]
; Name: "{group}\Servy"; Filename: "{app}\Servy.exe"; IconFilename: "{app}\servy.ico"; WorkingDir: "{app}"
; Name: "{group}\Servy Manager"; Filename: "{app}\Servy.Manager.exe"; IconFilename: "{app}\servy.ico"; WorkingDir: "{app}"

Name: "{commonprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Components: install_main_app
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Components: install_main_app

Name: "{commonprograms}\{#MyAppName}\{#ManagerAppName}"; Filename: "{app}\{#ManagerAppExeName}"; Components: install_manager
Name: "{commondesktop}\{#ManagerAppName}"; Filename: "{app}\{#ManagerAppExeName}"; Tasks: desktopicon; Components: install_manager

Name: "{commonprograms}\{#MyAppName}\{cm:UninstallShortcut}"; Filename: "{uninstallexe}";

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifsilent unchecked; Components: install_main_app
; Filename: "{app}\{#ManagerAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(ManagerAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifsilent unchecked; Components: install_manager
Filename: "{#DocsURL}"; Description: "{cm:OpenDocumentation}"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im ""{#MyAppExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopMainApp
Filename: "{sys}\taskkill.exe"; Parameters: "/im ""{#ManagerAppExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopManagerApp
Filename: "{sys}\taskkill.exe"; Parameters: "/im ""{#CliExeName}"" /t /f"; Flags: runhidden waituntilterminated; RunOnceId: StopCliApp

[UninstallDelete]
; Type: filesandordirs; Name: "{app}\taskschd"

[Code]
// -----------------------------------------------------
// At least one component is required
// -----------------------------------------------------
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = wpSelectComponents then
  begin
    if not WizardIsComponentSelected('install_main_app') and
       not WizardIsComponentSelected('install_cli') and
       not WizardIsComponentSelected('install_manager') then
    begin
      MsgBox(CustomMessage('SelectAtLeastOneComponent'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// -----------------------------------------------------
// Pre-Install actions:
//  - Check if a version is already installed
//  - Prepare install
// -----------------------------------------------------
function QueryUninstallRegistry(const AppIdGuidStr, ValueName: String): String;
var
  sUnInstPath, sValue, FormattedAppId: String;
begin
  sValue := '';

  // Ensure AppId is wrapped in curly braces {...}
  FormattedAppId := AppIdGuidStr;
  if (Length(FormattedAppId) > 0) and (FormattedAppId[1] <> '{') then
    FormattedAppId := '{' + FormattedAppId + '}';

  sUnInstPath := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\' + FormattedAppId + '_is1';

  // 1. Check 64-bit HKLM
  if not RegQueryStringValue(HKLM64, sUnInstPath, ValueName, sValue) then
  begin
    // 2. Check 32-bit HKLM (HKLM32 automatically redirects SOFTWARE to WOW6432Node).
    // HKCU is deliberately omitted: this setup runs elevated, so it writes to HKLM.
    // Probing HKCU could allow non-elevated user processes to spoof an uninstall path (#7484).
    RegQueryStringValue(HKLM32, sUnInstPath, ValueName, sValue);
  end;

  Result := sValue;
end;

function QueryUninstallValueAnyArch(const ValueName: String): String;
var
  AppIdGuids: array[0..1] of String;
  i: Integer;
  sResult: String;
begin
  Result := '';
  AppIdGuids[0] := '{#AppIdGuid}';
  if AppIdGuids[0] = '{#AppIdGuidArm64}' then
    AppIdGuids[1] := '{#AppIdGuidX64}'
  else
    AppIdGuids[1] := '{#AppIdGuidArm64}';

  for i := 0 to High(AppIdGuids) do
  begin
    sResult := QueryUninstallRegistry(AppIdGuids[i], ValueName);
    if sResult <> '' then
    begin
      Result := sResult;
      Exit;
    end;
  end;
end;

function GetUninstallString(): String;
begin
  Result := QueryUninstallValueAnyArch('UninstallString');
end;

function GetInstalledVersion(): String;
begin
  Result := QueryUninstallValueAnyArch('DisplayVersion');
end;

function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

function BoolToStr(Value: Boolean): String;
begin
  if Value then
  begin
    Result := 'True';
  end
  else
  begin
    Result := 'False';
  end;
end;

function UnInstallOldVersion(): Integer;
var
  sUnInstallString, sExePath: String;
  iResultCode: Integer;
begin
  Result := 0;
  sUnInstallString := GetUninstallString();
  Log('Uninstalling old version: ' + sUnInstallString);

  if sUnInstallString <> '' then
  begin
    sExePath := RemoveQuotes(sUnInstallString);
    if FileExists(sExePath) then
    begin
      if Exec(sExePath, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode) and (iResultCode = 0) then
        Result := 3
      else
        Result := 2;
    end
    else
    begin
      Log('Uninstall string executable does not exist: ' + sExePath);
      Result := 2;
    end;
  end
  else
    Result := 1;

  Log('UnInstallOldVersion.Result = ' + IntToStr(Result));
end;

function NumericVersion(const Version: string): Int64;
var
  Parts: TStringList;
  Major, Minor, Patch: Integer;
begin
  Parts := TStringList.Create;
  try
    Parts.StrictDelimiter := True;
    Parts.Delimiter := '.';
    Parts.DelimitedText := Version;

    Major := 0;
    Minor := 0;
    Patch := 0;

    if Parts.Count > 0 then Major := StrToIntDef(Parts[0], 0);
    if Parts.Count > 1 then Minor := StrToIntDef(Parts[1], 0);
    if Parts.Count > 2 then Patch := StrToIntDef(Parts[2], 0);

    Result := Int64(Major) * 1000000 + Int64(Minor) * 1000 + Int64(Patch);
  finally
    Parts.Free;
  end;
end;

function InitializeSetup(): Boolean;
var
  sInstalledVersion, message: String;
  installedVersion, myAppVersion: Int64;
  v: Integer;
begin
  Result := True;

  // Check for architecture mismatch to provide a clear error message (#7481)
  #if Arch == "arm64"
    if not IsArm64 then
    begin
      MsgBox(FmtMessage(CustomMessage('Arm64ArchMismatchError'), [ExpandConstant('{#MyAppVersion}')]), mbError, MB_OK);
      Result := False;
      Exit;
    end;
  #else
    if IsArm64 then
    begin
      // Soft warning for running x64 installer under ARM64 emulation
      if WizardSilent then
      begin
        Log('Running the x64 installer on an ARM64 system; continuing because setup is silent.')
      end
      else if MsgBox(FmtMessage(CustomMessage('X64OnArm64Warning'), [ExpandConstant('{#MyAppVersion}')]), mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
  #endif

  sInstalledVersion := GetInstalledVersion();

  if IsUpgrade() and (sInstalledVersion <> '') then
  begin
    Log('InitializeSetup.InstalledVersion: ' + sInstalledVersion);
    installedVersion := NumericVersion(sInstalledVersion);
    myAppVersion :=  NumericVersion(ExpandConstant('{#MyAppVersion}'));
    message := '';

    if installedVersion < myAppVersion  then
    begin
      message := CustomMessage('UpgradePrompt');
    end
    else if installedVersion > myAppVersion then
    begin
      message := CustomMessage('DowngradePrompt');
    end
    else
    begin
      message := CustomMessage('ReinstallPrompt');
    end;

    if WizardSilent then
    begin
      // Auto-accept in silent mode
      v := IDYES;
    end
    else
    begin
      // Interactive mode: show dialog
      v := MsgBox(message, mbInformation, MB_YESNO);
    end;

    if v <> IDYES then
    begin
      Result := False;
    end;
  end;
end;

function GetFileAttributes(lpFileName: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

// FILE_ATTRIBUTE_REPARSE_POINT is predefined by Inno Setup
const
  INVALID_FILE_ATTRIBUTES = $FFFFFFFF;

// Runs icacls.exe on the service data directory and reports whether it succeeded
function RunIcacls(const Params: string): Boolean;
var
  ResultCode: Integer;
begin
  Log('Securing service data directory: icacls.exe ' + Params);
  Result := Exec(ExpandConstant('{sys}\icacls.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if not Result then
    Log(Format('ERROR: icacls.exe failed with exit code %d.', [ResultCode]));
end;

// Secures the service data directory before anything is installed (#7526). Returns an empty string on success,
// otherwise the reason setup must stop: setup fails closed rather than install into a vault it could not secure.
function SecureDataDirectory(): String;
var
  DataDir, Params: string;
  Attributes: Cardinal;
begin
  Result := '';
  DataDir := ExpandConstant('{commonappdata}\Servy');

  if not DirExists(DataDir) and not ForceDirectories(DataDir) then
  begin
    Log(Format('ERROR: Failed to create the service data directory "%s".', [DataDir]));
    Result := FmtMessage(CustomMessage('CreateDataDirFailed'), [DataDir]);
    Exit;
  end;

  // A linked directory would have its owner and ACL written on a directory outside it: refuse it with a warning
  Attributes := GetFileAttributes(DataDir);
  if (Attributes <> INVALID_FILE_ATTRIBUTES) and ((Attributes and FILE_ATTRIBUTE_REPARSE_POINT) <> 0) then
  begin
    Log(Format('WARNING: the service data directory "%s" is a reparse point (symlink/junction); its owner and permissions were left unchanged.', [DataDir]));
    if not WizardSilent then
      MsgBox(FmtMessage(CustomMessage('DataDirReparsePointWarning'), [DataDir]), mbInformation, MB_OK);
    Exit;
  end;

  // 1. Hand the directory to Administrators: a standard user who created it before Servy did stays its owner
  //    otherwise, and an owner can always rewrite the DACL
  Params := Format('"%s" /setowner *S-1-5-32-544', [DataDir]);
  if not RunIcacls(Params) then
  begin
    Result := FmtMessage(CustomMessage('DataDirOwnershipFailed'), [DataDir]);
    Exit;
  end;

  // 2. Reset inheritance, grant Admins/System, and PURGE broad groups
  // *S-1-5-32-544: Administrators
  // *S-1-5-18:     Local System
  // *S-1-5-32-545: Users (Purge)
  // *S-1-5-11:     Authenticated Users (Purge)
  // *S-1-1-0:      Everyone (Purge)
  //
  // NOTE: No personal user ACE is granted here because an elevated installer's user is always covered by the
  // BUILTIN\Administrators grant. Every other explicit entry, on this directory and on everything in it, is purged by
  // SecurityHelper.CreateSecureDirectory and ServyExePermissionsHardener.SecureVaultAsync on the first elevated start
  // of Servy, Servy Manager or the CLI, which grant the custom service accounts their access back right after; setup
  // cannot read their accounts, so it leaves the entries below this directory to them.
  Params := Format('"%s" /inheritance:r /grant:r *S-1-5-32-544:(OI)(CI)F *S-1-5-18:(OI)(CI)F /remove:g *S-1-5-32-545 /remove:g *S-1-5-11 /remove:g *S-1-1-0 /remove:g "%s"', [DataDir, ExpandConstant('{username}')]);
  if not RunIcacls(Params) then
    Result := FmtMessage(CustomMessage('DataDirPermissionsFailed'), [DataDir]);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  sNeedsRestart : String;
begin
  sNeedsRestart := BoolToStr(NeedsRestart);
  Log('PrepareToInstall(' + sNeedsRestart + ') called');

  // Secure the service data directory before anything changes, so a failure leaves the machine as it was
  Result := SecureDataDirectory();
  if Result <> '' then
  begin
    if not WizardSilent then
      MsgBox(Result, mbError, MB_OK);
    Exit;
  end;

  if IsUpgrade() then
  begin
    if UnInstallOldVersion() <> 3 then
    begin
      if not WizardSilent then
        MsgBox(CustomMessage('UninstallPreviousFailed'), mbError, MB_OK);

      Result := CustomMessage('UninstallPreviousFailed');
      Exit;
    end;
  end;
  Result := '';
end;

// -----------------------------------------------------
// Post-Install actions:
//  - Refresh icon cache after install
//  - Clean up the remembered setup state (selected tasks, components, setup type)
//  - Add Servy to PATH if related task selected
//  - Replace XML Placeholders for Task Scheduler paths
// -----------------------------------------------------
// Declare Windows API function for refreshing icon cache
procedure SHChangeNotify(wEventId, uFlags: LongWord; dwItem1, dwItem2: LongWord); external 'SHChangeNotify@shell32.dll stdcall';

// Refresh icon cache after install
procedure RefreshIconCache();
begin
  SHChangeNotify($8000000, $0, 0, 0); // SHCNE_ASSOCCHANGED = $8000000
end;

const
  WM_SETTINGCHANGE = $001A;
  SMTO_ABORTIFHUNG = $0002;

function SendMessageTimeout(hWnd: LongWord; Msg: LongWord; wParam: LongWord;
  lParam: string; fuFlags: LongWord; uTimeout: LongWord; var lpdwResult: LongWord): LongWord;
  external 'SendMessageTimeoutW@user32.dll stdcall';

procedure RefreshEnvironment;
var
  ResultCode: LongWord;
begin
  SendMessageTimeout(
    HWND_BROADCAST,
    WM_SETTINGCHANGE,
    0,
    'Environment',        // pass string directly
    SMTO_ABORTIFHUNG,
    5000,
    ResultCode
  );
end;

// Removes trailing backslash
function NormalizeFolder(const S: string): string;
begin
  Result := S;
  if (Length(Result) > 0) and (Result[Length(Result)] = '\') then
    SetLength(Result, Length(Result) - 1);
end;

procedure SplitPath(const Value: string; Parts: TStringList);
begin
  Parts.StrictDelimiter := True;
  Parts.Delimiter := ';';
  Parts.DelimitedText := Value;
end;

function IsSameFolder(const Entry, NormalizedFolder: string): Boolean;
begin
  Result := CompareText(NormalizeFolder(Trim(Entry)), NormalizedFolder) = 0;
end;

function PathContainsFolder(const OldPath, NormalizedFolder: string): Boolean;
var
  Parts: TStringList;
  i: Integer;
begin
  Result := False;
  Parts := TStringList.Create;
  try
    SplitPath(OldPath, Parts);
    for i := 0 to Parts.Count - 1 do
      if IsSameFolder(Parts[i], NormalizedFolder) then
      begin
        Result := True;
        Exit;
      end;
  finally
    Parts.Free;
  end;
end;

procedure AddToPath(const Folder: string);
var
  OldPath, NewPath, NormalizedFolder: string;
  Parts: TStringList;
  i: Integer;
begin
  // Read the current system PATH
  if not RegQueryStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', OldPath) then
    OldPath := '';

  NormalizedFolder := NormalizeFolder(Folder);

  Parts := TStringList.Create;
  try
    SplitPath(OldPath, Parts);

    // Drop empty elements the existing value may already carry
    for i := Parts.Count - 1 downto 0 do
      if Trim(Parts[i]) = '' then
        Parts.Delete(i);

    if not PathContainsFolder(OldPath, NormalizedFolder) then
    begin
      Parts.Add(NormalizedFolder);
      NewPath := Parts.DelimitedText;

      // Write the new system PATH preserving REG_EXPAND_SZ type
      if not RegWriteExpandStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', NewPath) then
      begin
        Log('Failed to update system PATH environment variable.');
        if not WizardSilent then
          MsgBox(CustomMessage('UpdateSystemPathFailed'), mbError, MB_OK);
        Exit;
      end;

      // Notify the system about the environment change
      RefreshEnvironment();
    end;
  finally
    Parts.Free;
  end;
end;

function XmlEscape(const S: string): string;
begin
  Result := S;
  StringChangeEx(Result, '&', '&amp;', True);   // must run first
  StringChangeEx(Result, '<', '&lt;', True);
  StringChangeEx(Result, '>', '&gt;', True);
end;

procedure CleanupOldSetupRegistryState();
var
  UninstKey: String;
  Hives: array[0..2] of Integer;
  Values: array[0..4] of String;
  i, j: Integer;
begin
  UninstKey := ExpandConstant('SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');

  Hives[0] := HKLM64;
  Hives[1] := HKLM32;
  Hives[2] := HKCU;

  Values[0] := 'Inno Setup: Selected Tasks';
  Values[1] := 'Inno Setup: Deselected Tasks';
  Values[2] := 'Inno Setup: Selected Components';
  Values[3] := 'Inno Setup: Deselected Components';
  Values[4] := 'Inno Setup: Setup Type';

  for i := 0 to High(Hives) do
    for j := 0 to High(Values) do
      if RegValueExists(Hives[i], UninstKey, Values[j]) then
        RegDeleteValue(Hives[i], UninstKey, Values[j]);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  InstallDir: string;
  FileLines: TArrayOfString;
  FilesToFix: array[0..1] of String;
  I, J: Integer;
  InstallPath, XmlInstallPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    // 1. Refresh icon cache
    RefreshIconCache();

    // 2. Clean up legacy remembered setup state post-install
    //    (the service data directory is secured before the installation, in PrepareToInstall)
    CleanupOldSetupRegistryState();

    // 3. Add to PATH logic (if selected)
    if WizardIsTaskSelected('addpath') and WizardIsComponentSelected('install_cli') then
    begin
      InstallDir := NormalizeFolder(ExpandConstant('{app}'));
      AddToPath(InstallDir);
      if not RegWriteDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', 1) then
        Log('WARNING: Failed to write AddedToPath registry marker.');
    end;

    // 4. Resolve Hardcoded Paths in Task Scheduler XMLs
    InstallPath := ExpandConstant('{app}');
    XmlInstallPath := XmlEscape(InstallPath);
    FilesToFix[0] := InstallPath + '\taskschd\ServyFailureNotification.xml';
    FilesToFix[1] := InstallPath + '\taskschd\ServyFailureEmail.xml';

    for I := 0 to High(FilesToFix) do
    begin
      if FileExists(FilesToFix[I]) then
      begin
        if LoadStringsFromFile(FilesToFix[I], FileLines) then
        begin
          for J := 0 to GetArrayLength(FileLines) - 1 do
          begin
            StringChangeEx(FileLines[J], '{SERVY_INSTALL_PATH}', XmlInstallPath, True);
          end;

          // Written as UTF-8 with a BOM, which Task Scheduler accepts for task XML.
          if not SaveStringsToUTF8File(FilesToFix[I], FileLines, False) then
            Log('Failed to save updated Task Scheduler XML: ' + FilesToFix[I]);
        end
        else
          Log('Failed to load Task Scheduler XML for substitution: ' + FilesToFix[I]);
      end;
    end;
  end;
end;

// -----------------------------------------------------
// Uninstall actions:
//  - Remove Servy from PATH if necessary
//  - Remove the Servy host service and the binaries in the data folder when no Servy-managed service remains
//  - Clean up marker registry keys
// -----------------------------------------------------
procedure RemoveFromPath(const Folder: string);
var
  OldPath, NewPath: string;
  Parts: TStringList;
  i, InitialCount: Integer;
  NormalizedFolder: string;
begin
  NormalizedFolder := NormalizeFolder(Folder);

  if RegQueryStringValue(
        HKLM64,
        'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
        'Path',
        OldPath) then
  begin
    Parts := TStringList.Create;
    try
      SplitPath(OldPath, Parts);
      InitialCount := Parts.Count;

      for i := Parts.Count - 1 downto 0 do
        if IsSameFolder(Parts[i], NormalizedFolder) then
          Parts.Delete(i);

      if Parts.Count <> InitialCount then
      begin
        NewPath := Parts.DelimitedText;

        if not RegWriteExpandStringValue(
          HKLM64,
          'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
          'Path',
          NewPath
        ) then
          Log('WARNING: Failed to write updated PATH environment variable during uninstall.');

        RegWriteDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', 0);

        RefreshEnvironment();
      end;
    finally
      Parts.Free;
    end;
  end;
end;

// -----------------------------------------------------
// The Servy host service ("Servy", Servy.Host.exe / Servy.Host.Net48.exe) is installed at run time by the
// Desktop App, Servy Manager and servy-cli, and every Servy service depends on it. It, and the binaries the
// apps extracted into the data folder, are removed only when no Servy-managed service remains: the services
// the user did not uninstall keep running after Servy is uninstalled, as before (#7293).
// -----------------------------------------------------

// Lower-case file name of the executable a service command line runs: the quoted part, or the first token
function CommandLineExeName(const CommandLine: String): String;
var
  S: String;
  P: Integer;
begin
  Result := '';
  S := Trim(CommandLine);
  if (Length(S) > 0) and (S[1] = '"') then
  begin
    Delete(S, 1, 1);
    P := Pos('"', S);
    if P = 0 then
      Exit;
    S := Copy(S, 1, P - 1);
  end
  else
  begin
    P := Pos(' ', S);
    if P > 0 then
      S := Copy(S, 1, P - 1);
  end;
  Result := Lowercase(ExtractFileName(S));
end;

// The service wrappers of the .NET 10 and the .NET Framework 4.8 build
function IsServyWrapperExe(const ExeName: String): Boolean;
begin
  Result := (ExeName = 'servy.service.exe') or (ExeName = 'servy.service.cli.exe')
    or (ExeName = 'servy.service.net48.exe') or (ExeName = 'servy.service.cli.net48.exe');
end;

function IsServyHostExe(const ExeName: String): Boolean;
begin
  Result := (ExeName = 'servy.host.exe') or (ExeName = 'servy.host.net48.exe');
end;

function DependsOnServyHost(const ServiceKey: String): Boolean;
var
  Dependencies: String;
begin
  Result := False;
  if RegQueryMultiStringValue(HKLM64, ServiceKey, 'DependOnService', Dependencies) then
    Result := Pos(#0 + 'servy' + #0, #0 + Lowercase(Dependencies) + #0) > 0;
end;

// True when a service runs a Servy wrapper or depends on the Servy host service. When the services
// cannot be listed, it answers True, so nothing is removed.
function ServyManagedServiceRemains(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  ServiceKey, ImagePath: String;
begin
  Result := True;
  if not RegGetSubkeyNames(HKLM64, 'SYSTEM\CurrentControlSet\Services', Names) then
  begin
    Log('Could not list the installed services; the Servy service and the extracted binaries are kept.');
    Exit;
  end;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if CompareText(Names[I], 'Servy') <> 0 then
    begin
      ServiceKey := 'SYSTEM\CurrentControlSet\Services\' + Names[I];
      if RegQueryStringValue(HKLM64, ServiceKey, 'ImagePath', ImagePath) and IsServyWrapperExe(CommandLineExeName(ImagePath)) then
      begin
        Log('Servy-managed service remains: ' + Names[I]);
        Exit;
      end;

      if DependsOnServyHost(ServiceKey) then
      begin
        Log('Service depending on the Servy service remains: ' + Names[I]);
        Exit;
      end;
    end;
  end;

  Result := False;
end;

// Deletes the files of one pattern directly in Folder (not in its subfolders)
procedure DeleteFilesMatching(const Folder, Pattern: String);
var
  FindRec: TFindRec;
begin
  if FindFirst(Folder + '\' + Pattern, FindRec) then
  begin
    try
      repeat
        if FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY = 0 then
        begin
          if DeleteFile(Folder + '\' + FindRec.Name) then
            Log('Deleted ' + Folder + '\' + FindRec.Name)
          else
            Log('Could not delete ' + Folder + '\' + FindRec.Name);
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure RemoveServyHostWhenUnused();
var
  ImagePath, DataDir: String;
  Attributes: Cardinal;
  ResultCode: Integer;
begin
  if ServyManagedServiceRemains() then
  begin
    Log('Servy-managed services remain; the Servy service and the binaries in the data folder are kept.');
    Exit;
  end;

  if RegQueryStringValue(HKLM64, 'SYSTEM\CurrentControlSet\Services\Servy', 'ImagePath', ImagePath) then
  begin
    // Never remove a service that only borrows the name (#7294)
    if IsServyHostExe(CommandLineExeName(ImagePath)) then
    begin
      // net stop waits for the service to stop, so its executable can be deleted afterwards
      Exec(ExpandConstant('{sys}\net.exe'), 'stop Servy', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      if Exec(ExpandConstant('{sys}\sc.exe'), 'delete Servy', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
        Log('Removed the Servy service.')
      else
        Log('Could not remove the Servy service. sc.exe exit code: ' + IntToStr(ResultCode));
    end
    else
      Log('The service named Servy runs ' + ImagePath + ', not the Servy host; it is left untouched.');
  end;

  // Executables and DLLs only: db\, security\, logs\ and the .json / .config files are kept
  DataDir := ExpandConstant('{commonappdata}\Servy');
  Attributes := GetFileAttributes(DataDir);
  if (Attributes = INVALID_FILE_ATTRIBUTES) or ((Attributes and FILE_ATTRIBUTE_REPARSE_POINT) <> 0) then
  begin
    Log('The service data directory "' + DataDir + '" is missing or a reparse point; its executables and DLLs are left untouched.');
    Exit;
  end;
  DeleteFilesMatching(DataDir, '*.exe');
  DeleteFilesMatching(DataDir, '*.dll');
end;

// PATH removal, Servy host removal and registry cleanup on uninstall
procedure CurUninstallStepChanged(Step: TUninstallStep);
var
  AddedToPath: Cardinal;
begin
  if Step = usUninstall then
  begin
    if RegQueryDWordValue(HKLM64, 'Software\Servy', 'AddedToPath', AddedToPath) then
    begin
      if AddedToPath = 1 then
      begin
        RemoveFromPath(ExpandConstant('{app}'));
        Log('RemoveFromPath("' + ExpandConstant('{app}') + '")');
      end;
    end;
  end;

  if Step = usPostUninstall then
  begin
    // After [UninstallRun] closed the apps
    RemoveServyHostWhenUnused();
    RegDeleteKeyIncludingSubkeys(HKLM64, 'Software\Servy');
  end;
end;
