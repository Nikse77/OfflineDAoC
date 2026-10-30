"""Owner 2026-10-31: puts the weapon reskin master (DOL.GS.WeaponReskinMaster) in Camelot, beside Lady Senia, the
Effect Master. The new row is a copy of Lady Senia's with its own name, class and a spot 150 units away.

    python -I add_weapon_reskin_master.py            (server must be stopped)
    python -I add_weapon_reskin_master.py restore    (removes the row it added)
    python -I add_weapon_reskin_master.py --db PATH  (another database file)
"""
import json, os, sqlite3, subprocess, sys, uuid

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.normpath(os.path.join(HERE, '..', '..', 'runtime', 'data', 'opendaoc.sqlite3.db'))
if '--db' in sys.argv:
    DB = os.path.abspath(sys.argv[sys.argv.index('--db') + 1])
BACKUP = os.path.join(os.path.dirname(DB), 'add_weapon_reskin_master.backup.json')
NAME = 'Master of Looks'


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
    if con.execute("SELECT 1 FROM Mob WHERE ClassType='DOL.GS.WeaponReskinMaster' AND Region=10").fetchone():
        sys.exit('Already there.')
    cols = [r[1] for r in con.execute('PRAGMA table_info(Mob)')]
    senia = con.execute("SELECT * FROM Mob WHERE Name='Lady Senia' AND Region=10").fetchone()
    if senia is None:
        sys.exit('Lady Senia not found in Camelot.')
    row = dict(zip(cols, senia))
    row.update(ClassType='DOL.GS.WeaponReskinMaster', Name=NAME, Guild='Weapon Reskin', X=row['X'] + 150,
               Mob_ID=str(uuid.uuid4()), LastTimeRowUpdated='2026-10-31 00:00:00')
    with con:
        con.execute(f"INSERT INTO Mob ({','.join(cols)}) VALUES ({','.join('?' * len(cols))})", [row[c] for c in cols])
    existing = json.load(open(BACKUP, encoding='utf-8')) if os.path.exists(BACKUP) else []
    json.dump(existing + [row['Mob_ID']], open(BACKUP, 'w', encoding='utf-8'), indent=1)
    print(f"added {NAME} at {row['X']},{row['Y']},{row['Z']} in region {row['Region']} (backup {os.path.basename(BACKUP)})")


if __name__ == '__main__':
    main()
