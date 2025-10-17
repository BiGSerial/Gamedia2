# Unity 2D Pixel Starter — Unity 6000.2.8f1 (Unity 6)

**Meta files**: Visible • **Serialization**: Force Text • **PPU**: 16 (sugerido) • **Pixel Perfect** ativo

## Como usar
1. Instale **Unity 6000.2.8f1** pelo Hub.
2. `git lfs install` (uma vez por máquina).
3. Crie o repo e faça o primeiro commit com estes arquivos.
4. Abra o projeto pelo **Unity Hub** (ele lerá `ProjectSettings/ProjectVersion.txt`).
5. No Unity, aplique nas *Project Settings*:
   - **Editor** → *Version Control*: **Visible Meta Files**
   - **Editor** → *Asset Serialization*: **Force Text**
   - **Physics 2D** → Gravity Y = -9.81
   - **Tags and Layers** → Sorting Layers: `Background`, `Midground`, `Player`, `Foreground`, `UI`
   - **Player** → *Active Input Handling*: **Input System (New)**
6. Instale os pacotes no Package Manager (versões compatíveis com 6000.2.8f1):
   - 2D Sprite, 2D Tilemap, 2D Pixel Perfect, Input System, TextMeshPro
   - (o Unity vai gerar `Packages/packages-lock.json` — **comite** após abrir o projeto)
7. Importe o **Pixel Adventure 1 & 2** para `Assets/PixelAdventure/` e confirme que os `.meta` foram criados.
8. Crie as cenas `Assets/_Project/Scenes/Main.unity` e `Level1.unity`, adicione em **Build Settings** e **comite**.

## Git hooks (opcional)
Ative hooks locais que impedem binários sem LFS:
```bash
git config core.hooksPath .githooks
chmod +x .githooks/pre-commit
```

## UnityYAMLMerge (opcional, recomendado)
Adicione no seu `.gitconfig` global/local:
```ini
[merge]
    tool = unityyamlmerge
[mergetool "unityyamlmerge"]
    cmd = 'C:/Program Files/Unity/Hub/Editor/6000.2.8f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
```

## Checklist antes de compartilhar com o time
- [ ] `ProjectSettings/ProjectVersion.txt` aponta **6000.2.8f1**
- [ ] Visible Meta Files + Force Text aplicados
- [ ] `Packages/manifest.json` e `packages-lock.json` comitados
- [ ] `.gitignore` e `.gitattributes` com LFS
- [ ] Hooks e UnityYAMLMerge configurados (opcional)
- [ ] Cenas adicionadas no **Build Settings**
- [ ] Pixel Perfect configurado na **Main Camera**