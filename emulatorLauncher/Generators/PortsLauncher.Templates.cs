namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private static string corsixth_hotkeys = 
            @"global_confirm = [[return]]
            global_confirm_alt = [[e]]
            global_cancel = [[escape]]
            global_cancel_alt = [[q]]
            global_fullscreen_toggle = {[[alt]],[[return]]}
            global_exitApp = {[[alt]],[[f4]]}
            global_resetApp = {[[shift]],[[f10]]}
            global_releaseMouse = {[[ctrl]],[[f10]]}
            global_connectDebugger = {[[ctrl]],[[c]]}
            global_showLuaConsole = [[f12]]
            global_runDebugScript = {[[shift]],[[d]]}
            global_screenshot = {[[ctrl]],[[s]]}
            global_stop_movie = [[escape]]
            global_window_close = [[escape]]
            global_stop_movie_alt = [[q]]
            global_window_close_alt = [[q]]
            ingame_scroll_up = [[up]]
            ingame_scroll_down = [[down]]
            ingame_scroll_left = [[left]]
            ingame_scroll_right = [[right]]
            ingame_scroll_shift = [[shift]]
            ingame_zoom_in = [[=]]
            ingame_zoom_in_more = {[[shift]],[[=]]}
            ingame_zoom_out = [[-]]
            ingame_zoom_out_more = {[[shift]],[[-]]}
            ingame_reset_zoom = [[0]]
            ingame_showmenubar = [[backspace]]
            ingame_showCheatWindow = [[f11]]
            ingame_loadMenu = {[[shift]],[[l]]}
            ingame_saveMenu = {[[shift]],[[s]]}
            ingame_jukebox = [[j]]
            ingame_openFirstMessage = [[m]]
            ingame_pause = [[p]]
            ingame_gamespeed_slowest = [[1]]
            ingame_gamespeed_slower = [[2]]
            ingame_gamespeed_normal = [[3]]
            ingame_gamespeed_max = [[4]]
            ingame_gamespeed_thensome = [[5]]
            ingame_gamespeed_speedup = [[z]]
            ingame_panel_bankManager = [[f1]]
            ingame_panel_bankStats = [[f2]]
            ingame_panel_staffManage = [[f3]]
            ingame_panel_townMap = [[f4]]
            ingame_panel_casebook = [[f5]]
            ingame_panel_research = [[f6]]
            ingame_panel_status = [[f7]]
            ingame_panel_charts = [[f8]]
            ingame_panel_policy = [[f9]]
            ingame_panel_map_alt = [[t]]
            ingame_panel_research_alt = [[r]]
            ingame_panel_casebook_alt = [[c]]
            ingame_panel_casebook_alt02 = {[[shift]],[[c]]}
            ingame_panel_buildRoom = [[f]]
            ingame_panel_furnishCorridor = [[g]]
            ingame_panel_editRoom = [[v]]
            ingame_panel_hireStaff = [[b]]
            ingame_rotateobject = [[space]]
            ingame_quickSave = {[[alt]],[[shift]],[[s]]}
            ingame_quickLoad = {[[alt]],[[shift]],[[l]]}
            ingame_restartLevel = {[[shift]],[[r]]}
            ingame_quitLevel = {[[shift]],[[q]]}
            ingame_setTransparent = [[x]]
            ingame_storePosition_1 = {[[alt]],[[1]]}
            ingame_storePosition_2 = {[[alt]],[[2]]}
            ingame_storePosition_3 = {[[alt]],[[3]]}
            ingame_storePosition_4 = {[[alt]],[[4]]}
            ingame_storePosition_5 = {[[alt]],[[5]]}
            ingame_storePosition_6 = {[[alt]],[[6]]}
            ingame_storePosition_7 = {[[alt]],[[7]]}
            ingame_storePosition_8 = {[[alt]],[[8]]}
            ingame_storePosition_9 = {[[alt]],[[9]]}
            ingame_storePosition_0 = {[[alt]],[[0]]}
            ingame_recallPosition_1 = {[[ctrl]],[[1]]}
            ingame_recallPosition_2 = {[[ctrl]],[[2]]}
            ingame_recallPosition_3 = {[[ctrl]],[[3]]}
            ingame_recallPosition_4 = {[[ctrl]],[[4]]}
            ingame_recallPosition_5 = {[[ctrl]],[[5]]}
            ingame_recallPosition_6 = {[[ctrl]],[[6]]}
            ingame_recallPosition_7 = {[[ctrl]],[[7]]}
            ingame_recallPosition_8 = {[[ctrl]],[[8]]}
            ingame_recallPosition_9 = {[[ctrl]],[[9]]}
            ingame_recallPosition_0 = {[[ctrl]],[[0]]}
            ingame_toggleAnnouncements = {[[alt]],[[a]]}
            ingame_toggleSounds = {[[alt]],[[s]]}
            ingame_toggleMusic = {[[alt]],[[m]]}
            ingame_toggleAdvisor = {[[shift]],[[a]]}
            ingame_toggleInfo = [[i]]
            ingame_poopLog = {[[ctrl]],[[d]]}
            ingame_poopStrings = {[[ctrl]],[[t]]}
            ingame_patient_gohome = [[h]]";

        private static string corsixth_config =
            @"fullscreen = true
            width = 800
            height = 600
            language = [=[french]=]
            audio = true
            free_build_mode = false
            play_sounds = true
            sound_volume = 0.5
            play_announcements = true
            announcement_volume = 0.5
            play_music = true
            music_volume = 0.5
            prevent_edge_scrolling = false
            capture_mouse = true
            right_mouse_scrolling = false
            adviser_disabled = false
            scrolling_momentum = 0.8
            twentyfour_hour_clock = true
            check_for_updates = false
            warmth_colors_display_default = 1
            grant_wage_increase = false
            movies = true
            play_intro = true
            play_demo = true
            allow_user_actions_while_paused = false
            volume_opens_casebook = false
            alien_dna_only_by_emergency = true
            alien_dna_must_stand = true
            alien_dna_can_knock_on_doors = false
            disable_fractured_bones_females = true
            enable_avg_contents = false
            remove_destroyed_rooms = false
            theme_hospital_install = [=[C:\RetroBat\roms\corsixth\HOSP.th]=]
            unicode_font = nil
            savegames = nil
            levels = nil
            campaigns = nil
            use_new_graphics = false
            new_graphics_folder = nil
            screenshots = nil
            audio_music = nil
            audio_frequency = 22050
            audio_channels = 2
            audio_buffer_size = 2048
            debug = false
            debug_falling = false
            idehost = nil
            ideport = nil
            idekey = nil
            transport = nil
            platform = nil
            workingdir = nil
            track_fps = false
            zoom_speed = 80
            scroll_speed = 2
            shift_scroll_speed = 4
            room_information_dialogs = true
            allow_blocking_off_areas = false
            direct_zoom = nil
            new_machine_extra_info = true
            player_name = [[]]";

        private static string cdogs_config =
            @"{
	            ""Version"": 8,
	            ""Game"": {
		            ""FriendlyFire"": true,
		            ""RandomSeed"": 0,
		            ""Difficulty"": ""Normal"",
		            ""FPS"": 70,
		            ""Superhot(tm)Mode"": false,
		            ""EnemyDensity"": 100,
		            ""NonPlayerHP"": 100,
		            ""PlayerHP"": 75,
		            ""Lives"": 2,
		            ""HealthPickups"": true,
		            ""Fog"": true,
		            ""SightRange"": 15,
		            ""FireMoveStyle"": ""Stop"",
		            ""SwitchMoveStyle"": ""Slide"",
		            ""AllyCollision"": ""Repel"",
		            ""LaserSight"": ""None""
                },
	            ""Deathmatch"": {
		            ""Lives"": 10
	            },
	            ""Dogfight"": {
                ""PlayerHP"": 100,
		            ""FirstTo"": 5

                },
	            ""Graphics"": {
                ""Brightness"": 0,
		            ""Fullscreen"": false,
		            ""WindowWidth"": 640,
		            ""WindowHeight"": 480,
		            ""ScaleFactor"": 2,
		            ""ShakeMultiplier"": 1,
		            ""ShowHUD"": true,
		            ""ScaleMode"": ""Nearest neighbor"",
		            ""Shadows"": true,
		            ""Gore"": ""Trickle"",
		            ""Brass"": true,
		            ""SecondWindow"": false

                },
	            ""Input"": {
                ""PlayerCodes0"": {
                    ""left"": 80,
			            ""right"": 79,
			            ""up"": 82,
			            ""down"": 81,
			            ""button1"": 27,
			            ""button2"": 29,
			            ""grenade"": 22,
			            ""map"": 4
                    },
		            ""PlayerCodes1"": {
                    ""left"": 92,
			            ""right"": 94,
			            ""up"": 96,
			            ""down"": 90,
			            ""button1"": 88,
			            ""button2"": 99,
			            ""grenade"": 91,
			            ""map"": 98

                    }
            },
	            ""Interface"": {
                ""ShowFPS"": false,
		            ""ShowTime"": false,
		            ""ShowHUDMap"": true,
		            ""AIChatter"": ""Seldom"",
		            ""Splitscreen"": ""Never"",
		            ""SplitscreenAI"": false

                },
	            ""Sound"": {
                ""MusicVolume"": 32,
		            ""SoundVolume"": 64,
		            ""Footsteps"": true,
		            ""Reloads"": true

                },
	            ""QuickPlay"": {
                ""MapSize"": ""Any"",
		            ""WallCount"": ""Any"",
		            ""WallLength"": ""Any"",
		            ""RoomCount"": ""Any"",
		            ""SquareCount"": ""Any"",
		            ""EnemyCount"": ""Any"",
		            ""EnemySpeed"": ""Any"",
		            ""EnemyHealth"": ""Any"",
		            ""EnemiesWithExplosives"": true,
		            ""ItemCount"": ""Any""

                },
	            ""StartServer"": false,
	            ""ListenPort"": 34219
            }";
    }
}
