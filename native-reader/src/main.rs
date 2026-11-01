use deadlock_reader::Reader;
use serde_json::json;
use std::{io::{self, Write}, thread, time::{Duration, Instant}};

// Read-only helper. Stdout is a private pipe to the parent; no listener, hooks or injection.
fn emit(value: serde_json::Value) -> bool {
    let mut out = io::stdout().lock();
    writeln!(out, "{value}").and_then(|_| out.flush()).is_ok()
}
fn main() {
    let once = std::env::args().any(|a| a == "--once");
    let session = format!("local-{}", std::process::id());
    let mut sequence = 0u64;
    loop {
        let reader = match Reader::attach() {
            Ok(r) => r,
            Err(e) => {
                let kind=if matches!(&e,deadlock_reader::Error::Memory(deadlock_memory::Error::ProcessNotFound(_))) {"idle"}else{"unavailable"};
                if !emit(json!({"kind":kind, "message":e.to_string()})) || once { return; }
                thread::sleep(Duration::from_secs(3)); continue;
            }
        };
        loop {
            let start = Instant::now();
            sequence += 1;
            let value = match reader.live_snapshot() {
                Ok(Some(s)) => {
                    let entities=reader.entities().ok();
                    let players: Vec<_> = s.scoreboard().map(|p| { let pdg=reader.field_addr(p.controller,"CCitadelPlayerController","m_PlayerDataGlobal"); json!({
                        "slot":p.slot,"team":p.team,"hero_id":p.hero_id,"steam_id":p.steam_id.map(|s|s.to_string()),
                        "name":p.name,"is_local":p.is_local,"is_observed":p.is_observed,
                        "hero_damage":p.hero_damage,"objective_damage":p.objective_damage,"healing":p.healing,
                        "kills":p.kills,"deaths":p.deaths,"assists":p.assists,"net_worth":p.net_worth,
                        "rejuvenator":p.has_rejuvenator,"rebirth":p.has_rebirth,"items":p.items,
                        "unspent_souls":pdg.and_then(|a|reader.field_u32(a,"PlayerDataGlobal_t","m_iGold")),
                        "lane":reader.field_u8(p.controller,"CCitadelPlayerController","m_nAssignedLane").filter(|v|*v>0&&*v<8).or_else(||reader.field_u8(p.controller,"CCitadelPlayerController","m_nOriginalLaneAssignment")),
                        "assigned_lane":reader.field_u8(p.controller,"CCitadelPlayerController","m_nAssignedLane"),
                        "lobby_slot":p.slot,
                        "hud_order":entities.as_ref().and_then(|e|e.find(|e|e.instance==p.controller)).map(|e|e.index)
                    })}).collect();
                    json!({"kind":"snapshot","session":session,"sequence":sequence,
                        "match_id":s.match_id,"phase":s.game_state.map(|g|g.name()),"context":s.context,
                        "spectator":!s.scoreboard().any(|p|p.is_local==Some(true)),
                        "viewed_hero":s.current_player().and_then(|p|p.hero_id),"seconds":s.clock.playing_seconds(),
                        "paused":s.paused,"winner":s.winning_team,"brawl":s.is_street_brawl(),
                        "boss_in":s.midboss_next_spawn_time.filter(|t|*t>0.0).zip(s.clock.now).map(|(t,now)|(t-now).max(0.0)),"boss_alive":s.midboss_alive(),
                        "players":players,"read_ms":start.elapsed().as_secs_f64()*1000.0})
                }
                Ok(None) => json!({"kind":"idle","session":session,"sequence":sequence}),
                Err(e) => { if !emit(json!({"kind":"unavailable","message":e.to_string()})) || once {return;} break; }
            };
            if !emit(value) || once { return; }
            thread::sleep(Duration::from_millis(100).saturating_sub(start.elapsed()));
        }
        thread::sleep(Duration::from_secs(3));
    }
}
