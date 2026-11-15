"""Makes a clean, vanilla-only test save for Protogen Systems Docking Solutions.

    python build-vanilla-test-world.py [--name "Docking Solutions Test (vanilla)"] [--steam <SteamID64>]

Run it with the game closed. It starts from the game's own Empty World (Content\\CustomWorlds\\Empty World): Creative,
no mods, no planets or asteroids. It makes you the template's player, turns on spectator, turns off encounters, and adds:
  - DOCK TEST Skiff (large grid, just ahead of you): cockpit, a nose Connector, a starboard Inset Connector, an LCD
    beside the cockpit, batteries, gyros and thrusters facing all six ways;
  - DOCK TEST Station (static, about 100 m ahead): Dock A (Connector), Dock B (Inset Connector),
    Dock C [NoDock] (Connector, should never be used), beacon, batteries, lights.
All blocks are vanilla and owned by you. An older save with the same name is renamed, never overwritten.
"""
import os, re, sys, uuid, random, shutil, subprocess, datetime

GAME = r'C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers'
TEMPLATE = os.path.join(GAME, 'Content', 'CustomWorlds', 'Empty World')
SAVES_ROOT = os.path.expandvars(r'%APPDATA%\SpaceEngineers\Saves')
TEMPLATE_CLIENT = '76561197967630685'
ME = '144115188075855895'          # the template's player identity (it has the character)
CRLF = '\r\n'

DIRS = {'Forward': (0, 0, -1), 'Backward': (0, 0, 1), 'Left': (-1, 0, 0), 'Right': (1, 0, 0), 'Up': (0, 1, 0), 'Down': (0, -1, 0)}
SIZES = {'LargeBlockArmorBlock': (1, 1, 1), 'LargeBlockCockpitSeat': (1, 1, 1), 'Connector': (1, 1, 1), 'LargeBlockInsetConnector': (1, 1, 1),
         'LargeBlockBatteryBlock': (1, 1, 1), 'LargeBlockGyro': (1, 1, 1), 'LargeBlockSmallThrust': (1, 1, 2), 'LargeLCDPanel': (1, 1, 1),
         'LargeBlockBeacon': (1, 2, 1), 'SmallLight': (1, 1, 1)}
TYPES = {'LargeBlockArmorBlock': 'CubeBlock', 'LargeBlockCockpitSeat': 'Cockpit', 'Connector': 'ShipConnector', 'LargeBlockInsetConnector': 'ShipConnector',
         'LargeBlockBatteryBlock': 'BatteryBlock', 'LargeBlockGyro': 'Gyro', 'LargeBlockSmallThrust': 'Thrust', 'LargeLCDPanel': 'TextPanel',
         'LargeBlockBeacon': 'Beacon', 'SmallLight': 'InteriorLight'}


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def cells(sub, at, fwd, up):
    """The grid cells a block fills: size x along its Right, y along its Up, z along its Forward."""
    sx, sy, sz = SIZES[sub]
    f, u = DIRS[fwd], DIRS[up]
    r = cross(f, u)
    ext = [abs(r[i]) * sx + abs(u[i]) * sy + abs(f[i]) * sz for i in range(3)]
    return [(at[0] + x, at[1] + y, at[2] + z) for x in range(ext[0]) for y in range(ext[1]) for z in range(ext[2])]


def eid():
    return str(random.randint(10 ** 17, 9 * 10 ** 17))


class Grid:
    def __init__(self, name):
        self.name, self.blocks, self.used = name, [], {}

    def add(self, sub, at, fwd='Forward', up='Up', name=None, extra=''):
        for c in cells(sub, at, fwd, up):
            assert c not in self.used, '%s: %s at %s overlaps %s' % (self.name, sub, c, self.used[c])
            self.used[c] = sub
        self.blocks.append((sub, at, fwd, up, name, extra))

    def fill(self, xs, ys, zs):
        for x in xs:
            for y in ys:
                for z in zs:
                    if (x, y, z) not in self.used:
                        self.add('LargeBlockArmorBlock', (x, y, z))

    def xml(self, pos, static):
        out = []
        for sub, at, fwd, up, name, extra in self.blocks:
            t = TYPES[sub]
            b = ['        <MyObjectBuilder_CubeBlock xsi:type="MyObjectBuilder_%s">' % t,
                 '          <SubtypeName>%s</SubtypeName>' % sub]
            if t != 'CubeBlock':
                b.append('          <EntityId>%s</EntityId>' % eid())
            b += ['          <Min x="%d" y="%d" z="%d" />' % at,
                  '          <BlockOrientation Forward="%s" Up="%s" />' % (fwd, up),
                  '          <ColorMaskHSV x="0" y="-0.8" z="-0.25" />' if t == 'CubeBlock' else '          <ColorMaskHSV x="0.08" y="0.2" z="0.05" />']
            if t != 'CubeBlock':
                b += ['          <Owner>%s</Owner>' % ME, '          <BuiltBy>%s</BuiltBy>' % ME, '          <ShareMode>Faction</ShareMode>']
                if name:
                    b.append('          <CustomName>%s</CustomName>' % name)
                b.append('          <Enabled>true</Enabled>')
                if extra:
                    b.append(extra)
            else:
                b.append('          <BuiltBy>%s</BuiltBy>' % ME)
            b.append('        </MyObjectBuilder_CubeBlock>')
            out.append(CRLF.join(b))
        head = '''    <MyObjectBuilder_EntityBase xsi:type="MyObjectBuilder_CubeGrid">
      <SubtypeName />
      <EntityId>%s</EntityId>
      <PersistentFlags>CastShadows InScene</PersistentFlags>
      <PositionAndOrientation>
        <Position x="%r" y="%r" z="%r" />
        <Forward x="0" y="0" z="-1" />
        <Up x="0" y="1" z="0" />
        <Orientation>
          <X>0</X>
          <Y>0</Y>
          <Z>0</Z>
          <W>1</W>
        </Orientation>
      </PositionAndOrientation>
      <LocalPositionAndOrientation xsi:nil="true" />
      <GridSizeEnum>Large</GridSizeEnum>
      <CubeBlocks>
''' % ((eid(),) + pos)
        tail = '''
      </CubeBlocks>
      <IsStatic>%s</IsStatic>
      <LinearVelocity x="0" y="0" z="0" />
      <AngularVelocity x="0" y="0" z="0" />
      <DisplayName>%s</DisplayName>
      <DestructibleBlocks>true</DestructibleBlocks>
      <IsRespawnGrid>false</IsRespawnGrid>
      <LocalCoordSys>0</LocalCoordSys>
      <TargetingTargets />
    </MyObjectBuilder_EntityBase>''' % ('true' if static else 'false', self.name)
        return head.replace('\n', CRLF) + CRLF.join(out) + tail.replace('\n', CRLF)


BATTERY = '          <CurrentStoredPower>3</CurrentStoredPower>\r\n          <ProducerEnabled>true</ProducerEnabled>\r\n          <MaxStoredPower>3</MaxStoredPower>\r\n          <ChargeMode>0</ChargeMode>'
BEACON = '          <BroadcastRadius>2000</BroadcastRadius>\r\n          <HudText>DOCK TEST Station</HudText>'


def skiff():
    g = Grid('DOCK TEST Skiff')
    g.add('LargeBlockCockpitSeat', (0, 1, -3), name='Cockpit')
    g.add('Connector', (0, 0, -4), name='Nose Connector')
    g.add('LargeBlockInsetConnector', (2, 0, 0), 'Right', name='Starboard Connector')
    g.add('LargeLCDPanel', (-1, 1, -4), 'Backward', name='LCD Dock')
    g.add('LargeBlockBatteryBlock', (0, 0, -1), name='Battery 1', extra=BATTERY)
    g.add('LargeBlockBatteryBlock', (-1, 0, -1), name='Battery 2', extra=BATTERY)
    g.add('LargeBlockGyro', (1, 0, -1), name='Gyro 1')
    g.add('LargeBlockGyro', (1, 1, -1), name='Gyro 2')
    # thrusters, one pair or one each way (exhaust facing out)
    g.add('LargeBlockSmallThrust', (-1, 0, 3), 'Backward', name='Thruster Main L')
    g.add('LargeBlockSmallThrust', (1, 0, 3), 'Backward', name='Thruster Main R')
    g.add('LargeBlockSmallThrust', (-1, 0, -5), 'Forward', name='Thruster Brake L')
    g.add('LargeBlockSmallThrust', (1, 0, -5), 'Forward', name='Thruster Brake R')
    g.add('LargeBlockSmallThrust', (-3, 0, 0), 'Left', name='Thruster Left')
    g.add('LargeBlockSmallThrust', (2, 0, 1), 'Right', name='Thruster Right')
    g.add('LargeBlockSmallThrust', (0, 2, 0), 'Up', name='Thruster Up')
    g.add('LargeBlockSmallThrust', (0, -2, 0), 'Down', name='Thruster Down')
    g.fill(range(-1, 2), range(0, 2), range(-3, 3))
    return g


def station():
    g = Grid('DOCK TEST Station')
    g.add('Connector', (-2, 0, 1), 'Backward', name='Dock A')
    g.add('LargeBlockInsetConnector', (2, 0, 1), 'Backward', name='Dock B')
    g.add('Connector', (0, 2, 1), 'Backward', name='Dock C [NoDock]')
    g.add('SmallLight', (-4, 3, 1), 'Backward', name='Light L')
    g.add('SmallLight', (4, 3, 1), 'Backward', name='Light R')
    g.add('LargeBlockBeacon', (0, 0, -1), 'Backward', name='DOCK TEST Station', extra=BEACON)
    g.add('LargeBlockBatteryBlock', (1, 0, -1), 'Backward', name='Station Battery 1', extra=BATTERY)
    g.add('LargeBlockBatteryBlock', (-1, 0, -1), 'Backward', name='Station Battery 2', extra=BATTERY)
    g.fill(range(-4, 5), range(-3, 4), range(0, 1))
    return g


def game_running():
    try:
        out = subprocess.run(['tasklist'], capture_output=True, text=True).stdout.lower()
        return 'spaceengineers.exe' in out or 'interim' in out
    except Exception:
        return False


def set_tag(s, tag, value):
    s2, n = re.subn(r'<%s>[^<]*</%s>' % (tag, tag), '<%s>%s</%s>' % (tag, value, tag), s)
    if n == 0:
        s2, n = re.subn(r'<%s ?/>' % tag, '<%s>%s</%s>' % (tag, value, tag), s)
    return s2


def main():
    a = sys.argv[1:]
    name = a[a.index('--name') + 1] if '--name' in a else 'Docking Solutions Test (vanilla)'
    steam = a[a.index('--steam') + 1] if '--steam' in a else None
    if not steam:
        ids = [d for d in os.listdir(SAVES_ROOT) if d.isdigit() and d.startswith('7656')]
        assert len(ids) == 1, 'pass --steam <SteamID64>; found ' + ', '.join(ids)
        steam = ids[0]
    if game_running():
        sys.exit('Close Space Engineers first.')
    assert os.path.isfile(os.path.join(TEMPLATE, 'Sandbox.sbc')), 'no Empty World template at ' + TEMPLATE
    target = os.path.join(SAVES_ROOT, steam, name)
    if os.path.exists(target):
        old = target + ' (old ' + datetime.datetime.now().strftime('%Y-%m-%d %H-%M-%S') + ')'
        os.rename(target, old)
        print('Kept the older save as', old)
    os.makedirs(target)
    for f in ('Sandbox.sbc', 'Sandbox_config.sbc', 'SANDBOX_0_0_0_.sbs', 'thumb.jpg'):   # not the .sbsB5: the XML must load
        shutil.copy2(os.path.join(TEMPLATE, f), os.path.join(target, f))

    world = str(uuid.uuid4())
    now = datetime.datetime.now().strftime('%Y-%m-%dT%H:%M:%S')
    for f in ('Sandbox.sbc', 'Sandbox_config.sbc'):
        p = os.path.join(target, f)
        s = open(p, encoding='utf-8').read()
        s = s.replace(TEMPLATE_CLIENT, steam)
        s = set_tag(s, 'SessionName', name)
        s = set_tag(s, 'WorldId', world)
        s = set_tag(s, 'LastSaveTime', now)
        s = set_tag(s, 'Description', 'Vanilla-only test world for Protogen Systems Docking Solutions (made by Tools/build-vanilla-test-world.py).')
        s = set_tag(s, 'EnableSpectator', 'true')
        for off in ('CargoShipsEnabled', 'EnableEncounters', 'EnableDrones', 'EnableWolfs', 'EnableSpiders', 'EnableContainerDrops', 'EnableEconomy'):
            s = set_tag(s, off, 'false') if '<%s>' % off in s else s
        assert '<Mods />' in s and '<ModItem' not in s, 'the template has mods?'
        open(p, 'w', encoding='utf-8', newline='').write(s)

    p = os.path.join(target, 'SANDBOX_0_0_0_.sbs')
    s = open(p, encoding='utf-8').read()
    grids = skiff().xml((0.0, 0.0, -25.0), False) + CRLF + station().xml((0.0, 0.0, -140.0), True) + CRLF
    k = s.index('  </SectorObjects>')
    s = s[:k] + grids + s[k:]
    open(p, 'w', encoding='utf-8', newline='').write(s)
    print('Made', target)
    print('Skiff: about 25 m from where you spawn, nose connector facing the station. Station: about 100 m beyond it, docks facing the skiff.')


if __name__ == '__main__':
    main()
