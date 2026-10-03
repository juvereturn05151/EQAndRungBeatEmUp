from pathlib import Path
import re, math, uuid
root = Path(__file__).resolve().parents[2]
# This one-time migration reads the old seconds-based assets before replacing their schema.
for relative in ['Assets/Scripts/Combat/AttackPlayer.cs','Assets/Scripts/Combat/CombatClock.cs','Assets/Editor/AttackDataEditor.cs']:
    path=root/relative
    if not Path(str(path)+'.meta').exists():
        Path(str(path)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
def guid(path):
    return re.search(r'guid: (\w+)', Path(str(path)+'.meta').read_text()).group(1)
for path in (root/'Assets/EQ_Rung_BeatEmUp/Attacks').glob('*.asset'):
    old=path.read_text()
    if '  frames:' in old: continue
    def scalar(name,default=0):
        m=re.search(r'^  '+name+r': ([^\n]+)',old,re.M)
        return float(m.group(1)) if m else default
    def vector(name,default):
        m=re.search(r'^  '+name+r': \{x: ([\d.\-]+), y: ([\d.\-]+)\}',old,re.M)
        return tuple(map(float,m.groups())) if m else default
    name=path.stem; air=name.startswith('Air'); enemy=name=='EnemyPunch'; launch=name=='Launcher'
    duration=math.ceil((scalar('startup')+scalar('activeDuration')+scalar('recovery'))*60-1e-6)
    start=math.ceil(scalar('startup')*60); end=math.ceil((scalar('startup')+scalar('activeDuration'))*60)
    cancel=math.ceil(scalar('comboWindowOpen')*60)
    prefix='BlueShirtGuy_AirAttack1_' if air else 'BlueShirtGuy_Launch1_' if launch else 'ThaiBadBoy_Attack1_' if enemy else 'BlueShirtGuy_Attack1_'
    folder=root/('Assets/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Attack1' if enemy else 'Assets/EQ_Rung_BeatEmUp/Sprites')
    sprites=sorted(p for p in folder.glob(prefix+'*.png') if re.search(r'_\d+$',p.stem))
    header=old.split('  animationState:')[0]
    lines=[header.rstrip(),f'  attackName: {name}',f'  domain: {int(air)}',f'  isLauncher: {int(launch)}','  cooldownFrames: 0',
           '  artworkNotes: '+('Reuses existing AirAttack1 artwork across all three air combo steps.' if air else 'Reuses existing Attack1 artwork across all three ground combo steps.' if not enemy and not launch else 'Uses existing project artwork.'),'  frames:']
    offset=vector('hitboxPosition',(.55,.55)); size=vector('hitboxSize',(1,1.1))
    for i in range(duration):
        sprite=sprites[min(len(sprites)-1,i*len(sprites)//duration)] if sprites else None
        lines.extend([f'  - sprite: {{fileID: 21300000, guid: {guid(sprite)}, type: 3}}' if sprite else '  - sprite: {fileID: 0}', '    hitboxes:' if start<=i<end else '    hitboxes: []'])
        if start<=i<end:
            lines.extend(['    - hitId: 0','      repeatAfterFrames: 0',f'      offset: {{x: {offset[0]}, y: {offset[1]}}}',f'      size: {{x: {size[0]}, y: {size[1]}}}',f'      laneTolerance: {scalar("laneTolerance",.55)}',f'      damage: {scalar("damage",10)}',f'      hitstunFrames: {math.ceil(scalar("hitstun",.3)*60)}','      hitstopFrames: 5',f'      knockback: {scalar("knockback",.25)}',f'      launchVelocity: {{x: {scalar("launchHorizontalForce") or .7}, y: {scalar("launchForce") or 8}}}',f'      hitType: {1 if launch else 2 if name=="AirPunch3" else 0}',f'      canHitGrounded: {int(not air)}',f'      canHitAirborne: {int(air or bool(scalar("canHitAirborne")))}'])
        lines.extend(['    movement: {x: 0, y: 0}','    setHorizontalVelocity: 0','    horizontalVelocity: 0','    setVerticalVelocity: 0','    verticalVelocity: 0','    verticalVelocityModifier: 0',f'    gravityScale: {.2 if air else 1}',f'    suspendFalling: {int(air)}',f'    canCancelIntoAttack: {int(i>=cancel and not launch and not enemy)}',f'    canCancelIntoLauncher: {int(i>=cancel and not launch and not air and not enemy)}',f'    canCancelIntoJump: {int(launch and i>=end)}','    invulnerable: 0','    superArmor: 0','    events: []'])
    path.write_text('\n'.join(lines)+'\n')
# Persist the new component on the existing prefabs, preserving every existing fileID.
for name in ['BlueShirtGuy','BadGuy']:
    path=root/f'Assets/EQ_Rung_BeatEmUp/Prefabs/{name}.prefab'; data=path.read_text()
    if 'BeatEmUp.AttackPlayer' in data: continue
    blocks=re.split(r'(?=--- !u!)',data)
    rootblock=next(b for b in blocks if f'  m_Name: {name}\n' in b and b.startswith('--- !u!1 '))
    rootid=re.search(r'--- !u!1 &(\d+)',rootblock).group(1)
    def component(typename):
        b=next(b for b in blocks if f'BeatEmUp.{typename}\n' in b)
        return re.search(r'--- !u!114 &(\d+)',b).group(1)
    newid='901000000000000001' if name=='BlueShirtGuy' else '901000000000000002'
    data=data.replace(rootblock,rootblock.replace('  m_Layer:',f'  - component: {{fileID: {newid}}}\n  m_Layer:',1))
    data+=f'''--- !u!114 &{newid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {rootid}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid(root/'Assets/Scripts/Combat/AttackPlayer.cs')}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::BeatEmUp.AttackPlayer
  motor: {{fileID: {component('CharacterMotor')}}}
  hitbox: {{fileID: {component('AttackHitbox')}}}
  animationDriver: {{fileID: {component('CharacterAnimation')}}}
'''
    mapping={'inputBufferDuration':('inputBufferFrames',60),'jumpBufferDuration':('jumpBufferFrames',60),'comboResetTime':('comboResetFrames',60),'finisherRecovery':('finisherRecoveryFrames',60),'maximumJuggleTime':('maximumJuggleFrames',60),'landingRecovery':('landingRecoveryFrames',60),'attackCooldown':('attackCooldownFrames',60)}
    for old,(new,fps) in mapping.items():
        data=re.sub(r'^  '+old+r': ([\d.]+)$',lambda m: f'  {new}: {round(float(m.group(1))*fps)}',data,flags=re.M)
    data=re.sub(r'^  launch(?:Horizontal)?Force: .*\n','',data,flags=re.M)
    path.write_text(data)
print('Migrated existing attack assets and both prefabs; GUIDs preserved.')
