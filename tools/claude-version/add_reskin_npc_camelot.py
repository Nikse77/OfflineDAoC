"""Owner 2026-10-31: puts a Reskin NPC (DOL.GS.ItemModel, the armour and melee-weapon skin master) in Camelot, 150 units
east of the Master of Looks, so the three looks services stand together: Lady Senia (colour and effect), the Master of
Looks (weapon models) and this one (armour skins). The row is a copy of Albion's Lord Fusin of Cathal Valley, moved to
Camelot (region 10, same height and heading as Lady Senia).

    python -I add_reskin_npc_camelot.py            (server must be stopped)
    python -I add_reskin_npc_camelot.py restore    (removes the row it added)
    python -I add_reskin_npc_camelot.py --db PATH  (another database file)
"""
import json, os, sqlite3, subprocess, sys, uuid

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.normpath(os.path.join(HERE, '..', '..', 'runtime', 'data', 'opendaoc.sqlite3.db'))
if '--db' in sys.argv:
    DB = os.path.abspath(sys.argv[sys.argv.index('--db') + 1])
BACKUP = os.path.join(os.path.dirname(DB), 'add_reskin_npc_camelot.backup.json')


def main():
    if 'coreserver' in subprocess.run(['tasklist'], capture_output=True, text=True).stdout.lower():
        sys.exit('Stop the server first.')
    con = sqlite3.connect(DB)
    if 'restore' in sys.argv:
        ids = json.load(open(BACKUP, encoding='utf-8'))
        with con:
            n = con.execute(f"DELETE FROM Mob WHERE Mob_ID IN ({','.join('?' * len(ids))})", ids).rowcount
        print(f'removed {n}')
        return
    if con.execute("SELECT 1 FROM Mob WHERE ClassType='DOL.GS.ItemModel' AND Region=10").fetchone():
        sys.exit('Already there.')
    cols = [r[1] for r in con.execute('PRAGMA table_info(Mob)')]
    fusin = con.execute("SELECT * FROM Mob WHERE Name='Lord Fusin' AND Region=165 AND Realm=1").fetchone()
    senia = con.execute("SELECT * FROM Mob WHERE Name='Lady Senia' AND Region=10").fetchone()
    master = con.execute("SELECT X FROM Mob WHERE ClassType='DOL.GS.WeaponReskinMaster' AND Region=10").fetchone()
    if fusin is None or senia is None:
        sys.exit('Lord Fusin of Cathal Valley or Lady Senia not found.')
    row = dict(zip(cols, fusin))
    base = dict(zip(cols, senia))
    row.update(Region=10, X=(master[0] if master else base['X'] + 150) + 150, Y=base['Y'], Z=base['Z'],
               Heading=base['Heading'], Mob_ID=str(uuid.uuid4()), LastTimeRowUpdated='2026-10-31 00:00:00')
    with con:
        con.execute(f"INSERT INTO Mob ({','.join(cols)}) VALUES ({','.join('?' * len(cols))})", [row[c] for c in cols])
    existing = json.load(open(BACKUP, encoding='utf-8')) if os.path.exists(BACKUP) else []
    json.dump(existing + [row['Mob_ID']], open(BACKUP, 'w', encoding='utf-8'), indent=1)
    print(f"added {row['Name']} at {row['X']},{row['Y']},{row['Z']} in region {row['Region']} (backup {os.path.basename(BACKUP)})")


if __name__ == '__main__':
    main()
