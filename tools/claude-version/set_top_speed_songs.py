"""Owner 2026-10-31: the three top group speed songs (Motivational Anthem 1105, Heavenly Song of Travel 3612, Clear Horizon 5155;
all three realms' bards) go from 204 to 250 (percent of the normal speed, Spell.Value). A trial, easy to undo: the old values are
kept in a small json beside the database, not a copy of the database.

    python -I set_top_speed_songs.py            (server must be stopped; sets 250)
    python -I set_top_speed_songs.py restore    (puts the old values back)
    python -I set_top_speed_songs.py --db PATH  (another database file)
    python -I set_top_speed_songs.py --value N  (another value than 250)
"""
import json, os, sqlite3, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.normpath(os.path.join(HERE, '..', '..', 'runtime', 'data', 'opendaoc.sqlite3.db'))
if '--db' in sys.argv:
    DB = os.path.abspath(sys.argv[sys.argv.index('--db') + 1])
VALUE = float(sys.argv[sys.argv.index('--value') + 1]) if '--value' in sys.argv else 250.0
BACKUP = os.path.join(os.path.dirname(DB), 'set_top_speed_songs.old_values.json')
SPELLS = (1105, 3612, 5155)


def main():
    if 'coreserver' in subprocess.run(['tasklist'], capture_output=True, text=True).stdout.lower():
        sys.exit('Stop the server first.')
    con = sqlite3.connect(DB)
    marks = ','.join('?' * len(SPELLS))
    if 'restore' in sys.argv:
        old = json.load(open(BACKUP, encoding='utf-8'))
        with con:
            for spell_id, value in old.items():
                con.execute('UPDATE Spell SET Value=? WHERE SpellID=?', (value, int(spell_id)))
        os.remove(BACKUP)
        print(f'restored {len(old)}: {old}')
        return
    rows = dict(con.execute(f'SELECT SpellID, Value FROM Spell WHERE SpellID IN ({marks}) AND Type=?', (*SPELLS, 'SpeedEnhancement')).fetchall())
    if len(rows) != len(SPELLS):
        sys.exit(f'Found only {sorted(rows)} of {SPELLS}.')
    old = json.load(open(BACKUP, encoding='utf-8')) if os.path.exists(BACKUP) else {}
    for spell_id, value in rows.items():
        old.setdefault(str(spell_id), value)      # the very first values are kept, whatever the number of runs
    json.dump(old, open(BACKUP, 'w', encoding='utf-8'), indent=1)
    with con:
        con.execute(f'UPDATE Spell SET Value=? WHERE SpellID IN ({marks})', (VALUE, *SPELLS))
    print(f'set {VALUE} on {len(SPELLS)} spells; old values {old} in {os.path.basename(BACKUP)}')


if __name__ == '__main__':
    main()
