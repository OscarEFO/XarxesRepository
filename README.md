# NetworkRepository
 
Repository for Network subject by:
- Pau Vivas
- Sergio Garriguez
- Oscar Escofet

This is our 1 vs 1 multiplayer spaceship game where a red and a green spaceship face off in a shooting game where they have to take out three lives from each other, while avoiding the falling asteroids. 
For this second delivery we reestructured the code to no longer use json packages and instead use a binary package system as instructed and we implemented the requirements of the world state replication. 
The clients passively replicate state from the server, and the packets send more than 3 types of data with player id, position, rotation, name, health... And data from the projectiles and asteroids, with also the option of those and the player being destroyed.
Replication manager is included in the server in the ServerUDP and ClientManagerUDP scripts.
The system supports 2 clients with the player1 and player 2 connecting, recieving each other's packets and replicating each other's states. 
And finally the communication is done using UDP simulated connections.

The game currently has a bug where the names are not properly put in the two players, player 1 is put under that name but player 2 defaults to the generic name "User" instead of player 2.
GitHub link: https://github.com/OscarEFO/XarxesRepository.git