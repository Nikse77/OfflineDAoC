"""Owner 2026-10-31: four more bows at Albion's weapons merchant next to the master trainer (list OrbsAlbionWeapons),
which sold only two bows. They keep the prices of their item templates (coins):
Dragonsworn Long Bow, Maleficent Darkspire Bow, Remorseless (level 50) and Duskwood Long Bow of the Chasm.

    python -I add_albion_bows.py            (server must be stopped)
    python -I add_albion_bows.py restore    (removes the rows it added, listed in add_albion_bows.backup.json)
    python -I add_albion_bows.py --db PATH  (another database file; the backup json goes beside it)
"""
import json, os, sqlite3, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.normpath(os.path.join(HERE, '..', '..', 'runtime', 'data', 'opendaoc.sqlite3.db'))
if '--db' in sys.argv:
    DB = os.path.abspath(sys.argv[sys.argv.index('--db') + 1])
BACKUP = os.path.join(os.path.dirname(DB), 'add_albion_bows.backup.json')
LIST = 'OrbsAlbionWeapons'
ITEMS = ('1Dragonsworn_Long_Bow', 'Maleficent_Darkspire_Bow', 'Remorseless_Lab_Alb', 'Duskwood_Long_Bow_of_the_Chasm')
PAGE = 0   # the page where the two bows already are


def main():
    if 'coreserver' in subprocess.run(['tasklist'], capture_output=True, text=True).stdout.lower():
        sys.exit('Stop the server first.')
    con = sqlite3.connect(DB)
    if 'restore' in sys.argv:
        ids = json.load(open(BACKUP, encoding='utf-8'))
        with con:
            n = con.execute(f"DELETE FROM MerchantItem WHERE MerchantItem_ID IN ({','.join('?' * len(ids))})", ids).rowcount
        print(f'removed {n}')
        return
    have = {r[0] for r in con.execute('SELECT ItemTemplateID FROM MerchantItem WHERE ItemListID=?', (LIST,))}
    missing = [i for i in ITEMS if i not in have]
    for item in missing:
        if con.execute('SELECT 1 FROM ItemTemplate WHERE Id_nb=?', (item,)).fetchone() is None:
            sys.exit(f'No item template {item}')
    used = {r[0] for r in con.execute('SELECT SlotPosition FROM MerchantItem WHERE ItemListID=? AND PageNumber=?', (LIST, PAGE))}
    free = [s for s in range(30) if s not in used]
    if len(free) < len(missing):
        sys.exit('No free slots on the page.')
    added = []
    with con:
        for item, slot in zip(missing, free):
            row_id = f'{LIST}-bow-{item}'
            con.execute('INSERT INTO MerchantItem (ItemListID, ItemTemplateID, PageNumber, SlotPosition, LastTimeRowUpdated, MerchantItem_ID)'
                        " VALUES (?, ?, ?, ?, datetime('now'), ?)", (LIST, item, PAGE, slot, row_id))
            added.append(row_id)
    existing = json.load(open(BACKUP, encoding='utf-8')) if os.path.exists(BACKUP) else []
    json.dump(existing + added, open(BACKUP, 'w', encoding='utf-8'), indent=1)
    print(f'added {len(added)}: {", ".join(missing)} (backup {os.path.basename(BACKUP)})')


if __name__ == '__main__':
    main()
