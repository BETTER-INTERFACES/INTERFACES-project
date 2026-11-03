model NBSSAM_model_VR

import "NBSS_AM_v2.1_forVR.gaml"

species unity_linker parent: abstract_unity_linker {
	string player_species <- string(unity_player);
	int max_num_players  <- 1;
	int min_num_players  <- 1;

	unity_property up_filter_media;
	unity_property up_rain;
	unity_property up_default;
	unity_property up_ponding_area;
	
	//Agents I've created for this project
	unity_property up_shrubs_plants;
	unity_property up_flower;
	unity_property up_trash;
	unity_property up_weeds;
	unity_property up_building;
	unity_property up_nbss_area;
	unity_property up_local_flora;

	bool do_send_world <- true;
	// Init position at crossroads
	list<point> init_locations <- [{100.0, 10.0, 0.0}];

	init {
		do define_properties;
		player_unity_properties <- [up_default];
		
		// Background objects to send to unity
		do add_background_geometries(building, up_building);
	}
	
	action define_properties {
		unity_aspect default_aspect <- geometry_aspect(1.0,#green,precision);
		up_default <- geometry_properties("default","",default_aspect,#no_interaction,false);
		unity_properties << up_default;

		/* Water flow */
		unity_aspect rain_aspect <- prefab_aspect("Prefabs/RainMaker/Prefab/RainPrefab",1.0,0.0,1.0,0.0,precision);
		up_rain <- geometry_properties("rain","rain",rain_aspect,#no_interaction,false);
		unity_properties << up_rain;
		
		
		/* NBSS components */
		unity_aspect nbss_area_aspect <- prefab_aspect("Prefabs/NBSSAreaPrefab",1.0,0.8,1.0,0.0,precision);
		up_nbss_area <- geometry_properties("nbss_area","nbss_area",nbss_area_aspect,#ray_interactable,false);
		unity_properties << up_nbss_area;
		
		unity_aspect filter_media_aspect <- prefab_aspect("Prefabs/FilterMediaPrefab",1.0,-2.35,1.0,90.0,precision); //geometry_aspect(1.5,#saddlebrown,precision);
		up_filter_media <- geometry_properties("filter_media","filter_media",filter_media_aspect,#ray_interactable,false);
		unity_properties << up_filter_media;
		
		unity_aspect ponding_area_aspect <- geometry_aspect(0.1, #blue, precision);
		up_ponding_area <- geometry_properties("ponding_area","ponding_area",ponding_area_aspect,#no_interaction,false);
		unity_properties << up_ponding_area;
		

		/* Vegetation */
		unity_aspect shrubs_aspect <- prefab_aspect("Prefabs/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs/Bush_1_1",1.0,0.0,1.0,0.0,precision);
		up_shrubs_plants <- geometry_properties("shrubs_plants","shrubs_plants",shrubs_aspect,#ray_interactable,false);
		unity_properties << up_shrubs_plants;
	
		unity_aspect flower_aspect <- prefab_aspect("Prefabs/DEMOLowPolyFlowers/Prefabs/SM_Dandelion_Small",1.0,0.0,1.0,0.0,precision);
		up_flower <- geometry_properties("flower","flower",flower_aspect,#no_interaction,false);
		unity_properties << up_flower;
		
		unity_aspect local_flora_aspect <- prefab_aspect("Cartoon_Farm_Crops/Prefabs/Standard/Eggplant_Plant",1.0,-2.0,1.0,0.0,precision);
		up_local_flora <- geometry_properties("local_flora","local_flora",local_flora_aspect,#no_interaction,false);
		unity_properties << up_local_flora;
		
		
		/* Trash/Invasive vegetation */
		unity_aspect trash_aspect <- geometry_aspect(0.5,#red,precision);
		//unity_aspect trash_aspect <- prefab_aspect("Prefabs/Mess Maker Free/Low Poly/Cans/Soda Can Green Crushed",1.0,0.0,1.0,0.0,precision); //pb avec ce prefab
		up_trash <- geometry_properties("trash","trash",trash_aspect,#ray_interactable,false);
		unity_properties << up_trash;
		
		unity_aspect weeds_aspect <- prefab_aspect("Prefabs/Parks And Nature Pack/Prefab/GrassE",1.0,0.0,1.0,0.0,precision);
		up_weeds <- geometry_properties("weeds","weeds",weeds_aspect,#ray_interactable,false);
		unity_properties << up_weeds;
		
		
		/* Urban environment */
		unity_aspect building_aspect <- geometry_aspect(10,#gray,precision);
		up_building <- geometry_properties("building","",building_aspect,#no_interaction,false);
		unity_properties << up_building;
	}
	
	// Dynamic data to send to Unity (interactable objects)
	reflex send_geometries {
		//do add_geometries_to_send(shrubs_plants, up_shrubs_plants);
		do add_geometries_to_send(trash, up_trash);
		do add_geometries_to_send(weeds, up_weeds);
		do add_geometries_to_send(flower, up_flower);
		do add_geometries_to_send(local_flora, up_local_flora);
	}
	
	// Send species attributes to Unity as they're updated
	reflex send_agents when: not empty(unity_player) {
		list<float> rain_intensity <- rain collect float(each.runoff.my_flow);
		list<string> rain_seasons <- rain collect current_season;
		list<int> fqt_fm <- filter_media collect (each.function_attributes["my_fqt"]);
		list<float> fm_sediments <- filter_media collect (each.partpoll_acc);
		list<float> water_level_pond <- ponding_area collect (each.water_level);
		list<float> health <- nbss_area collect (each.health);
		
		map<string,list<unknown>> atts_fm <-  [
			"fqt_fm":: fqt_fm,
			"sediments_fm":: fm_sediments
		]; 
		map<string,list<unknown>> atts_rain <- [
			"rain_intensity":: rain_intensity,
			"rain_seasons":: rain_seasons
		];
		map<string,list<unknown>> atts_ponding_area <- ["water_level":: water_level_pond];
		map<string,list<unknown>> atts_nbss_area <- ["health":: health];
		
		//do add_geometries_to_send(rain,up_rain,atts_rain);
		do add_geometries_to_send(ponding_area, up_ponding_area, atts_ponding_area);
		do add_geometries_to_send(filter_media, up_filter_media, atts_fm);
		do add_geometries_to_send(nbss_area, up_nbss_area, atts_nbss_area);
		
	}
	
	/*Interactions Unity-Gama*/
	
	// Send message to Unity
	reflex send_message when: send_message {
		//write "Send message: ";
		//do send_message players: unity_player as list mes: ["message_init"::"Mmmh certaines noues semblent ne pas fonctionner correctement..."];
		do send_message players: unity_player as list mes: messages;
		send_message <- false;
		messages <- [];
	}
	
	// Receive message from Unity
	action receive_message(string id, string mes) {
		write "Player " + id + " send the message: " + mes;
		if (mes = "skip") {
			do_skip <- true; 
		}
		else if (mes = "scenario0") {
			launch_sc0 <- true;
		}
		else if (mes = "scenario1") {
			launch_sc1 <- true;
		}
		else if (mes = "scenario2") {
			launch_sc2 <- true;
		}
		else if (mes contains ":") { //Each swale has a dedicated health
			list<string> name_health <- mes split_with ":";
            nbss_area nbs <- nbss_area first_with (each.name = name_health at 0);
            ask nbs {
            	health <- health + float(name_health at 1);
            }
		}
		else { //We handle the score at the end of active phase
			score <- float(mes);
			score <- float(mes replace (",", "."));
			write "score : " + score;
		}
	}
	
	//Add water to swale if is obstructed
	reflex is_flooding when: ponding_area one_matches (each.is_obstructed) {
		ask ponding_area where (each.is_obstructed) {
			if ( water_level < 0.5) {
				water_level <- water_level + 0.05;
			}
		}
	}
	
	//Scenario 1 interactions
	action maintenance_remove(string id) {
		agent ag <- (trash + weeds) first_with (each.name = id) ;
		if (ag != nil) {
			ask (trash + weeds) {
	            remove key: self from: myself.geometries_to_send;
	            do die;
	        }
	        ask nbss_area where (each.my_name = "nbss_area0") {
				health <- health + 45.0;
			}
			ask ponding_area where (each.my_name = "ponding_area0" or each.my_name = "ponding_area2" or each.my_name = "ponding_area3") {
				water_level <- water_level - 0.25;
			}
			messages <- messages + ["init_":: "regular_state"];
			messages <- messages + ["message_":: "Moins de déchets = moins de pollution pour l’eau et le sol."];
			send_message <- true;
		}
	}
	
	action curage(string id) {
		filter_media fm <- (filter_media first_with (each.name = id));
		if (fm != nil) {
			ask fm {
				if (function_attributes["my_fqt"] <= 1) {
					function_attributes["my_fqt"] <- 2;
					partpoll_acc <- 0.0;
					ask ponding_area where (each.is_obstructed or each.my_name = "ponding_area2" or each.my_name = "ponding_area3") { // condition à modif selon si transit ou non, et chemin de l'eau
						is_obstructed <- false;
						water_level <- water_level - 0.25;
					}
					ask nbss_area where (each.my_name = "nbss_area0") {
						health <- health + 45.0;
					}
					ask nbss_area where (each.my_name = "nbss_area2" or each.my_name = "nbss_area3") {
						health <- health + 40.0;
					}
					//score <- score + 30.0;
					//weight_score <- 30;
					//messages <- messages + ["init_":: "regular_state"];
					messages <- messages + ["add_to_score":: "45.0"];
					messages <- messages + ["message_":: "Parfait ! On a enlevé l'accumulation de sédiments, l'eau peut de nouveau s'infilter dans les sols."];
					send_message <- true;	
				}
				else {
					messages <- messages + ["message_":: "Rien ne se passe... Le problème vient sûrement d'une autre noue."];
					send_message <- true;	
				}
			}
		}
	}
	
	//Scenario 2 interactions
	action water_late_early(string id) {
		filter_media fm <- (filter_media first_with (each.name = id));
		if (fm != nil) {
			ask ponding_area where (each.my_NBSS = fm.my_NBSS) {
				water_level <- water_level + 0.2;
			}
			ask nbss_area where (each.my_NBSS = fm.my_NBSS) {
				health <- health + 15.0;
			}
			//weight_score <- 15;
			messages <- messages + ["add_to_score":: string(5)];
			messages <- messages + ["message_":: "Arroser aux heures plus fraîches permet de préserver ce qu'il reste de biodiversité... Mais attention à ne pas gaspiller d'eau !"];
			send_message <- true;
		}
	}
	action planter_flore_locale(string id) {
		filter_media ag_fm <- filter_media first_with (each.name = id);
	    
	    NBSS target_nbss <- nil;
	    if (ag_fm != nil) {
	        target_nbss <- ag_fm.my_NBSS;
	    }
	    
	    if (target_nbss != "") {
	        list<filter_media> fm_valides <- filter_media where (each.my_NBSS = target_nbss);
	        
	        agent zone_cible <- one_of(fm_valides);
	        
	        if (zone_cible != nil) {
	            create local_flora number: 25 {
	                location <- any_location_in(zone_cible.shape);
	            }
	        }
	        ask nbss_area where (each.my_NBSS = target_nbss) {
	        	health <- health + 3.0;
	        }
			messages <- messages + ["add_to_score":: string(25)];
			messages <- messages + ["message_":: "Ces plantes supportent la sécheresse et aident la noue à survivre grâce à leurs racines profondes."];
			send_message <- true;
	    }	
	}
	action plant_flowers(string id) {
		nbss_area ag <- nbss_area first_with (each.name = id);
	    
	    NBSS target_nbss <- nil;
	    if (ag != nil) {
	        target_nbss <- ag.my_NBSS;
	    }
	    if (target_nbss != "") {
	    	point loc <- target_nbss.location;
	    	geometry geom <- target_nbss.shape;
	    	create flower number: 10 {
		    	shape <- triangle(0.6);
		    	if (geom.width > geom.height) {
		    		location <- {loc.x - geom.width/2 + (index mod 5) * geom.width/4,
						loc.y + (index < 5 ? 2.5 : -2.5)
					};
		    	}
		    	else {
		    		location <- {loc.x + ((index mod 5) < 5 ? 2.5 : -2.5),
						loc.y - geom.height/2 + (index mod 5) * geom.height/4
					};
		    	}
			ask nbss_area where (each.my_NBSS = target_nbss) {
	        	health <- health - 3.0;
	        }
			messages <- messages + ["add_to_score":: string(-5)];
			messages <- messages + ["message_":: "Jolies ! Mais... Est-ce vraiment utile ?"];
			send_message <- true;
	    	}
		}
	}
}



species unity_player parent: abstract_unity_player{
	float player_size <- 1.0;
	rgb color <- #red;
	float cone_distance <- 10.0 * player_size;
	float cone_amplitude <- 90.0;
	float player_rotation <- 90.0;
	bool to_display <- true;
	float z_offset <- 2.0;
	aspect default {
		if to_display {
			if selected {
				 draw circle(player_size) at: location + {0, 0, z_offset} color: rgb(#blue, 0.5);
			}
			draw circle(player_size/2.0) at: location + {0, 0, z_offset} color: color ;
			draw player_perception_cone() color: rgb(color, 0.5);
		}
	}
}

experiment vr_xp parent:"Interface (EN)" autorun: true type: unity {
	float minimum_cycle_duration <- 0.1;
	string unity_linker_species <- string(unity_linker);
	list<string> displays_to_hide <- ["Rain", "Inlet", "Ponding area", "Vegetation cover", "Filter media", "Performance", map];
	float t_ref;

	action create_player(string id) {
		ask unity_linker {
			do create_player(id);
		}
	}

	action remove_player(string id_input) {
		if (not empty(unity_player)) {
			ask first(unity_player where (each.name = id_input)) {
				do die;
			}
		}
	}

	output {
		 
		 display map_VR parent: map {

			 species unity_player;
			 event #mouse_down{
				 float t <- gama.machine_time;
				 if (t - t_ref) > 500 {
					 ask unity_linker {
						 move_player_event <- true;
					 }
					 t_ref <- t;
				 }
			 }
		 }
	}
}
