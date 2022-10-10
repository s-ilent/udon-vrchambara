# Udon VRChambara
This is the repo for my basic swordfighting system I made in UdonSharp.

I can't say it's well designed or anything, but it sure is fun ingame! Also, I can't guarantee that the repo will immediately work in your project. If there's further interest, I can see about polishing it up, but for now this is provided more for curiosity.

## Info
This system is made up of 2 main components: the AttackManager, and Entities.

Entities check if they've struck a player, and if so, call on the AttackManager to apply the hit effect. The hit effect is driven by an animation which calls the functions to immobilise the player, control a screen effect, and then apply velocity to the player. The goal here is not realism, but something like Super Smash Bros - you hit someone, they get locked in place, and then fly off. Boom! 

Hits contain their velocity data and players are flung back away from their opponent based on the attacker's velocity. In the ideal case, velocity would be driven by the direction and momentum of the attacker's swing, but [this doesn't seem to work](https://twitter.com/TheOtaking/status/1578691637403852800). In case people are hit multiple times while immobilised, the velocity is cumlative and cleared only once it is applied.

Defending players have authority over whether they get hit or not, making the fighting more like fencing than anything else. When attacking players swipe, they will hear the weapon swing sound effect if it collides with the remote player, but the remote player will only play the getting hit sound effect if they are hit. 

If a player is not holding a weapon, they will not react to being hit, with two exceptions. Players who drop their weapon are given a cooldown period where they can still be attacked. An entity can also be set to attack non-combatantsm, which is mainly useful for environmental hazards. 

The script also depends on Udon Airtime. This is for gameplay purposes - Airtime makes movement feel smoother, and being able to control Airtime parameters is very useful. Here, the design is set so that combatants can not double jump (as it gives them too much movement in midair). However, the integration is something that could be made optional. 

The hit effect is driven by an AnimationClip in an Animator, as those are fairly reliable systems that don't cost script execution time. I think. The clip allows for tuning the timing of hits and effects easily, and Unity doesn't seem to skip the animation events, so that's all good! In my maps, I've used this to control a PostProcess Volume which cranks up the contrast a ton, which really sells the brief pause at the moment of impact.

## License

This code is under MIT license.